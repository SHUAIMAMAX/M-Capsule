using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace PocketGrid
{
    internal sealed class Manager : IDisposable
    {
        public readonly Preferences Preferences;
        public readonly ShortcutStore Files = new ShortcutStore(Path.Combine(Settings.DataDirectory,"Capsules"));
        public readonly Dictionary<string,CapsuleWindow> Windows = new Dictionary<string,CapsuleWindow>();
        public GridWindow Popup { get; private set; }
        public readonly IconCache Icons;
        public int Dialogs;
        public readonly HwndSource Controller;
        readonly Forms.NotifyIcon tray;
        readonly Native.WinEvent foreground;
        readonly Native.WinEvent desktopChange;
        readonly DispatcherTimer desktopRecovery;
        readonly DispatcherTimer desktopGridTimer;
        readonly DispatcherTimer shellRecovery;
        readonly DispatcherTimer copyMigration;
        readonly Queue<Tuple<Capsule,Shortcut>> pendingCopies=new Queue<Tuple<Capsule,Shortcut>>();
        int shellAttempts;
        readonly Native.MouseHook outside;
        readonly EventWaitHandle quitEvent;
        readonly RegisteredWaitHandle quitWait;
        IntPtr foregroundHook, minimizeHook, hideHook, mouseHook;
        int recoveryTicks;
        TaskbarController taskbar;
        bool disposed;
        bool freshPlacementPending;
        bool organizing;
        int taskbarErrorPending;
        DateTime lastGridRefresh=DateTime.MinValue;
        public Manager()
        {
            Preferences=Settings.Load();
            Lang.Current=Preferences.Language;
            freshPlacementPending=Settings.FreshStart&&Preferences.Capsules.Count>0;
            foreach(var c in Preferences.Capsules)foreach(var entry in c.Items.Where(x=>x.IsReference))pendingCopies.Enqueue(Tuple.Create(c,entry));
            Icons=new IconCache(Application.Current.Dispatcher);
            Directory.CreateDirectory(Settings.DataDirectory);
            Controller=new HwndSource(new HwndSourceParameters(RuntimeMode.ControllerTitle) { Width=1,Height=1,WindowStyle=0 });
            Controller.AddHook(Messages);
            foreground = OnForeground;
            desktopChange=OnDesktopChange;
            desktopRecovery=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(100)};
            desktopRecovery.Tick+=(s,e)=>RecoverDesktop();
            desktopGridTimer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(2)};
            desktopGridTimer.Tick+=(s,e)=>{if(Native.DesktopForeground())RefreshDesktopGrid();};
            shellRecovery=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(500)};
            copyMigration=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(100)};
            copyMigration.Tick+=(s,e)=>
            {
                if(pendingCopies.Count==0){copyMigration.Stop();return;}
                var next=pendingCopies.Dequeue();
                try
                {
                    if(Files.MigrateSmallReference(next.Item1,next.Item2))
                    {
                        Settings.Save(Preferences);
                        if(Popup!=null&&Popup.Capsule.Id==next.Item1.Id)Popup.Refresh();
                    }
                }
                catch(Exception ex){StartupLog.Write("small-file migration skipped "+ex.Message);}
            };
            shellRecovery.Tick+=(s,e)=>
            {
                bool ready=Windows.Count>0;
                foreach(var w in Windows.Values)
                    if(w.IsVisible&&!w.Interacting&&!Native.DesktopLayer(w.Handle,true))ready=false;
                if(ready||++shellAttempts>=120)
                {
                    shellRecovery.Stop();
                    StartupLog.Write("desktop initialization ready="+ready+" attempts="+shellAttempts);
                }
            };
            outside = OnMouse;
            if(!RuntimeMode.Sandbox)
            {
                foregroundHook=Native.SetWinEventHook(3,3,IntPtr.Zero,foreground,0,0,2);
                minimizeHook=Native.SetWinEventHook(0x16,0x16,IntPtr.Zero,desktopChange,0,0,0);
                hideHook=Native.SetWinEventHook(0x8003,0x8003,IntPtr.Zero,desktopChange,(uint)Process.GetCurrentProcess().Id,0,0);
            }
            // Show anchors before shell COM, tray icon extraction, or taskbar initialization.
            foreach(var c in Preferences.Capsules)CreateWindow(c);
            if(!RuntimeMode.Sandbox)desktopGridTimer.Start();
            tray=new Forms.NotifyIcon { Text=Ui.Product };
            tray.ContextMenuStrip=BuildTray();
            tray.MouseDoubleClick+=(s,e)=>{if(Preferences.Capsules.Count>0)Open(Preferences.Capsules[0]);};
            if(!RuntimeMode.Sandbox&&Windows.Count>0)shellRecovery.Start();
            quitEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\"+RuntimeMode.Identity+".Exit");
            quitWait=ThreadPool.RegisterWaitForSingleObject(quitEvent,(s,t)=>Application.Current.Dispatcher.BeginInvoke(new Action(Exit)),null,Timeout.Infinite,false);
            Application.Current.Dispatcher.BeginInvoke(new Action(()=>
            {
                if(disposed)return;
                tray.Icon=System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
                tray.Visible=true;
                try { Settings.SetStartup(Preferences.StartWithWindows); }
                catch(Exception ex) { StartupLog.Write("registration failure "+ex);Notify("开机自启登记检查失败，请使用托盘菜单检查/修复："+ex.Message); }
                if(Preferences.TaskbarMode!="normal")ApplyTaskbar(false);
                Save();
            }),DispatcherPriority.ContextIdle);
            if(Settings.LoadNotice!=null)Application.Current.Dispatcher.BeginInvoke(new Action(()=>MessageBox.Show(Settings.LoadNotice,Ui.Product,MessageBoxButton.OK,MessageBoxImage.Information)));
        }
        void CreateWindow(Capsule c,bool delayReveal=false)
        {
            var w=new CapsuleWindow(this,c);
            if(delayReveal)w.Opacity=0;
            if(Windows.Count==0)w.ContentRendered+=(s,e)=>{Program.LogFirstFrame();if(freshPlacementPending)PlaceByDesktopIcons(c,false);RefreshDesktopGrid();if(pendingCopies.Count>0)copyMigration.Start();};
            Windows[c.Id]=w;w.Show();w.ApplyDesktopSize(DesktopIconGrid.InWindowCoordinates(w));w.UpdateIconOcclusion();
        }
        public void Error(Exception ex) { ClosePopup(); MessageBox.Show(ex.Message,Ui.Product,MessageBoxButton.OK,MessageBoxImage.Information); }
        public void Save()
        {
            try{Settings.Save(Preferences);}catch(Exception ex){Error(new IOException("配置暂时未能保存："+ex.Message));}
        }
        public void Notify(string message)
        {
            if(disposed)return;
            Application.Current.Dispatcher.BeginInvoke(new Action(()=>{if(!disposed)tray.ShowBalloonTip(6000,Ui.Product,message,Forms.ToolTipIcon.Info);}));
        }
        Forms.ContextMenuStrip BuildTray()
        {
            var m=new Forms.ContextMenuStrip();
            m.Items.Add(Lang.Text("◆ 胶囊管理 · Capsule Manager","◆ Capsule Manager","◆ カプセル管理"),null,(s,e)=>ShowCapsuleList());
            var appearance=new Forms.ToolStripMenuItem(Lang.Text("任务栏外观","Taskbar appearance","タスクバーの外観"));
            foreach(var pair in new[]{new[]{"normal",Lang.Text("正常","Normal","標準")},new[]{"acrylic",Lang.Text("毛玻璃","Blur","ぼかし")},new[]{"clear",Lang.Text("全透明","Transparent","透明")}})
            {
                string mode=pair[0];var item=new Forms.ToolStripMenuItem(pair[1]){Checked=Preferences.TaskbarMode==mode};
                item.Click+=(s,e)=>{SetTaskbarMode(mode);};appearance.DropDownItems.Add(item);
            }
            m.Items.Add(appearance);
            m.Items.Add(Lang.Text("终极整理模式…","Ultimate organizing mode…","究極整理モード…"),null,async (s,e)=>await RunUltimateOrganizer());
            m.Items.Add(new Forms.ToolStripSeparator());
            var startup=new Forms.ToolStripMenuItem(Lang.Text("开机自动启动","Start with Windows","Windows と同時に起動")) { Checked=Preferences.StartWithWindows };
            startup.Click+=(s,e)=>{try{bool enabled=!Preferences.StartWithWindows;Settings.SetStartup(enabled);Preferences.StartWithWindows=enabled;Save();startup.Checked=enabled;}catch(Exception ex){Error(ex);}};
            m.Items.Add(startup);
            m.Items.Add(Lang.Text("检查/修复开机自启…","Check/repair startup…","自動起動を確認・修復…"),null,(s,e)=>
            {
                try
                {
                    Settings.SetStartup(true);Preferences.StartWithWindows=true;Save();RefreshTray();
                    MessageBox.Show(Lang.Text("已创建并读回核验登录自启快捷方式：\n","Sign-in startup shortcut created and verified:\n","サインイン時の起動ショートカットを作成・確認しました：\n")+Settings.StartupLink+
                        Lang.Text("\n\n目标：","\n\nTarget: ","\n\n対象：")+Settings.Executable+
                        Lang.Text("\n配置：","\nConfiguration: ","\n設定：")+Settings.DataDirectory+
                        Lang.Text("\n\n如果 Windows 设置中禁用了此启动项，请在‘应用 → 启动’中启用。启动过程记录在 Data\\Logs\\startup.log。",
                        "\n\nIf Windows disabled this startup item, enable it in Settings → Apps → Startup. See Data\\Logs\\startup.log.",
                        "\n\nWindows で無効になっている場合は、設定 → アプリ → スタートアップで有効にしてください。記録：Data\\Logs\\startup.log。"),Ui.Product);
                }
                catch(Exception ex){Error(ex);}
            });
            m.Items.Add(new Forms.ToolStripSeparator());
            m.Items.Add(Lang.Text("帮助","Help","ヘルプ"),null,(s,e)=>{try{Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"使用说明.txt")){UseShellExecute=true});}catch(Exception ex){Error(ex);}});
            m.Items.Add(Lang.Text("退出","Exit","終了"),null,(s,e)=>Exit());
            return m;
        }
        public void RefreshTray()
        {
            var old=tray.ContextMenuStrip;tray.ContextMenuStrip=BuildTray();
            if(old!=null)Application.Current.Dispatcher.BeginInvoke(new Action(()=>old.Dispose()),DispatcherPriority.Background);
        }
        public void SetLanguage(string value)
        {
            string previous=Preferences.Language;
            Preferences.Language=Lang.Normalize(value);
            try{Settings.Save(Preferences);}
            catch(Exception ex){Preferences.Language=previous;Error(new IOException("语言设置未能保存："+ex.Message,ex));return;}
            Lang.Current=Preferences.Language;
            ClosePopup();
            foreach(var window in Windows.Values)window.UpdateLanguage();
            RefreshTray();
        }
        void ReportTaskbarError(string message)
        {
            if(disposed||Interlocked.Exchange(ref taskbarErrorPending,1)!=0)return;
            Application.Current.Dispatcher.BeginInvoke(new Action(()=>
            {
                if(disposed){Interlocked.Exchange(ref taskbarErrorPending,0);return;}ClosePopup();Dialogs++;
                try{MessageBox.Show(message,Ui.Product+" · 任务栏切换失败",MessageBoxButton.OK,MessageBoxImage.Warning);}finally{Dialogs--;Interlocked.Exchange(ref taskbarErrorPending,0);}
            }));
        }
        void ApplyTaskbar(bool reset)
        {
            if(RuntimeMode.Sandbox){RuntimeMode.Log("TASKBAR simulated "+Preferences.TaskbarMode);return;}
            if(taskbar==null && Preferences.TaskbarMode!="normal")taskbar=new TaskbarController(ReportTaskbarError);
            if(taskbar!=null)taskbar.Apply(Preferences.TaskbarMode,reset);
        }
        IntPtr Messages(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled)
        {
            if(msg==Native.Wake){Application.Current.Dispatcher.BeginInvoke(new Action(()=>{if(Preferences.Capsules.Count>0)Open(Preferences.Capsules[0]);}));handled=true;}
            if(msg==Native.WM_COPYDATA)
            {
                var data=(Native.CopyData)Marshal.PtrToStructure(lp,typeof(Native.CopyData));
                if(data.Tag==new IntPtr(0x4D534243)&&data.Size==66)
                {
                    string id=Marshal.PtrToStringUni(data.Data,32);
                    if(Logic.IsId(id))Application.Current.Dispatcher.BeginInvoke(new Action(()=>OpenById(id)));
                }
                handled=true;return new IntPtr(1);
            }
            if((uint)msg==Native.TaskbarCreated)
            {
                tray.Visible=false;tray.Visible=true;
                Application.Current.Dispatcher.BeginInvoke(new Action(()=>{RefreshDesktopGrid();Reposition();ApplyTaskbar(true);}));
            }
            if(msg==0x007E || msg==0x001A || msg==0x031E)
                Application.Current.Dispatcher.BeginInvoke(new Action(()=>{RefreshDesktopGrid();Reposition();if(Preferences.TaskbarMode!="normal")ApplyTaskbar(false);}));
            return IntPtr.Zero;
        }
        void OnForeground(IntPtr hook,uint evt,IntPtr hwnd,int obj,int child,uint thread,uint time)
        {
            if(disposed)return;
            Application.Current.Dispatcher.BeginInvoke(new Action(()=>
            {
                if(disposed)return;
                bool desktop=Native.DesktopForeground();
                foreach(var w in Windows.Values)if(w.IsVisible&&!w.Interacting)Native.DesktopLayer(w.Handle,desktop);
                if(desktop)RefreshDesktopGrid();
            }));
        }
        void OnDesktopChange(IntPtr hook,uint evt,IntPtr hwnd,int obj,int child,uint thread,uint time)
        {
            if(disposed||obj!=0||child!=0)return;
            // A hidden capsule or a window being minimized can be part of Show Desktop.
            // Retry briefly after the shell finishes its window operation.
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if(disposed)return;
                if(evt==0x8003 && !Windows.Values.Any(w=>w.Handle==hwnd))return;
                recoveryTicks=15;
                RecoverDesktop();
                desktopRecovery.Start();
            }),DispatcherPriority.Input);
        }
        void RecoverDesktop()
        {
            if(disposed||--recoveryTicks<=0){desktopRecovery.Stop();return;}
            foreach(var w in Windows.Values)
                if(w.IsVisible&&!w.Interacting && Native.IsWindow(w.Handle))Native.DesktopLayer(w.Handle,true);
        }
        IntPtr OnMouse(int code,IntPtr wp,IntPtr lp)
        {
            if(code>=0 && wp.ToInt32()==0x0201 && Popup!=null && Dialogs==0 && !Popup.MenuOpen)
            {
                var current=Popup;
                var p=(Native.Point)Marshal.PtrToStructure(lp,typeof(Native.Point));Native.Rect rect;
                if(Native.GetWindowRect(current.Handle,out rect) && (p.X<rect.Left||p.X>=rect.Right||p.Y<rect.Top||p.Y>=rect.Bottom))
                    Application.Current.Dispatcher.BeginInvoke(new Action(()=>{if(Popup==current)ClosePopup();}));
            }
            return Native.CallNextHookEx(mouseHook,code,wp,lp);
        }
        public void OpenById(string id)
        {
            var c=Preferences.Capsules.FirstOrDefault(x=>x.Id==id);
            if(c==null){Notify("这个胶囊已删除。");return;}
            Open(c);
        }
        public void Open(Capsule c,bool dragHover=false)
        {
            if(disposed)return;
            if(Popup!=null && Popup.Capsule.Id==c.Id){if(!dragHover)ClosePopup();return;}
            ClosePopup();
            Popup=new GridWindow(this,c,Windows[c.Id]){DragHover=dragHover,ShowActivated=!dragHover};Popup.Show();
            if(!dragHover){Popup.Activate();Popup.Focus();Native.SetForegroundWindow(Popup.Handle);}
            if(!RuntimeMode.Sandbox)mouseHook=Native.SetWindowsHookEx(14,outside,Native.GetModuleHandle(null),0);
        }
        public void ClosePopup()
        {
            if(mouseHook!=IntPtr.Zero){Native.UnhookWindowsHookEx(mouseHook);mouseHook=IntPtr.Zero;}
            var old=Popup;Popup=null;if(old!=null)old.Close();
        }
        public void Reposition(){ClosePopup();foreach(var c in Preferences.Capsules){var w=Windows[c.Id];w.ClampPosition();w.UpdateIconOcclusion();if(w.IsVisible)Native.DesktopLayer(w.Handle,Native.DesktopForeground());}Save();}
        void RefreshDesktopGrid(bool force=false)
        {
            if(RuntimeMode.Sandbox||disposed||(!force&&(DateTime.UtcNow-lastGridRefresh).TotalSeconds<1.5))return;
            lastGridRefresh=DateTime.UtcNow;
            DesktopIconGrid.Refresh((spacingChanged,iconsChanged)=>
            {
                if(disposed)return;
                if(freshPlacementPending){PlaceByDesktopIcons(Preferences.Capsules[0],false);freshPlacementPending=false;}
                if(spacingChanged)
                {
                    foreach(var w in Windows.Values)w.ApplyDesktopSize(DesktopIconGrid.InWindowCoordinates(w));
                    Reposition();
                }
                else if(iconsChanged)foreach(var w in Windows.Values)w.UpdateIconOcclusion();
            });
        }
        public void ShowCapsuleList()
        {
            ClosePopup();Dialogs++;
            try{new CapsuleListDialog(this).ShowDialog();}finally{Dialogs--;}
        }
        public void NewCapsule()
        {
            if(Preferences.Capsules.Count>=Logic.MaxCapsules){Notify(Lang.Text("最多创建 32 个胶囊。","Up to 32 capsules are supported.","カプセルは最大 32 個です。"));return;}
            ClosePopup();Dialogs++;
            string name;try{name=Ui.AskName(Lang.Text("新建胶囊","New capsule","新しいカプセル"),Lang.Text("小工具","Tools","ツール"));}finally{Dialogs--;}
            if(name==null)return;
            CreateCapsule(name);
        }
        internal Capsule CreateCapsule(string name)
        {
            if(Preferences.Capsules.Count>=Logic.MaxCapsules)throw new IOException("最多创建 32 个胶囊。");
            var c=new Capsule{Name=Logic.CleanName(name),ColorIndex=Preferences.NextColor++,X=32+Preferences.Capsules.Count%6*92,Y=140+Preferences.Capsules.Count/6*110};
            Preferences.Capsules.Add(c);CreateWindow(c);PlaceByDesktopIcons(c,true);Save();if(Windows[c.Id].IsVisible)Native.DesktopLayer(Windows[c.Id].Handle,true);RefreshTray();RefreshDesktopGrid(true);return c;
        }
        async Task RunUltimateOrganizer()
        {
            if(organizing)return;
            string folder=null;
            try
            {
                if(Preferences.Capsules.Count>Logic.MaxCapsules-5)throw new IOException(Lang.Text("需要预留 5 个胶囊位置，请先删减胶囊后再整理。","Free five capsule slots before organizing.","整理の前にカプセルを 5 個分空けてください。"));
                var plan=UltimateOrganizer.Prepare(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
                string prompt=Lang.Text(
                    "为了极致整理桌面，现在会把您桌面上所有文件和快捷方式先收纳到一个桌面的文件夹里，可以吗？\n\n"+
                    "将移动当前用户桌面的 "+plan.Files.Length+" 个普通文件到"+(plan.DestinationExists?"已有的":"新建的")+"“胶囊整理”，不移动已有文件夹、隐藏或系统文件，也不移动公共桌面项目。"+
                    (plan.DestinationExists?"如遇同名文件，会自动添加序号，不覆盖旧文件。":"")+
                    "然后打开该文件夹并排列在屏幕左半边，在右半边新增 5 个胶囊。\n\n此操作不会在开机时自动运行。",
                    "Organize your desktop now?\n\nMove "+plan.Files.Length+" visible files and shortcuts from your personal Desktop into "+(plan.DestinationExists?"the existing":"a new")+" '胶囊整理' folder. Existing folders, hidden/system files and Public Desktop items stay where they are. Names that already exist get a number; nothing is overwritten. The folder will open on the left and five new capsules will appear on the right.\n\nThis never runs automatically at startup.",
                    "デスクトップを整理しますか？\n\n個人用デスクトップの表示中のファイルとショートカット "+plan.Files.Length+" 件を「胶囊整理」フォルダーへ移動します。既存フォルダー、非表示・システム項目、共有デスクトップは対象外です。同名ファイルは番号を付け、上書きしません。フォルダーを左側に開き、右側に新しいカプセルを 5 個作成します。\n\n起動時に自動実行されません。");
                if(MessageBox.Show(prompt,Ui.Product,MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
                organizing=true;
                folder=await Task.Run(()=>UltimateOrganizer.Execute(plan,true));
                var screen=Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
                bool arranged=false;
                try{arranged=await ExplorerLayout.OpenOnLeft(folder,screen);}
                catch(Exception ex){StartupLog.Write("Explorer half-screen layout failed: "+ex.Message);}
                CreateFiveOrganizingCapsules(screen);
                if(!arranged)MessageBox.Show(Lang.Text("文件已安全收纳在“胶囊整理”，5 个胶囊也已生成。文件夹窗口未能自动排列到左半屏，请手动打开或调整窗口。",
                    "Files are in '胶囊整理' and five capsules were created. Explorer could not be placed on the left; please arrange it manually.",
                    "ファイルを「胶囊整理」に収め、カプセルを 5 個作成しました。Explorer を左側に配置できませんでした。手動で調整してください。"),Ui.Product);
            }
            catch(Exception ex){Error(folder==null?ex:new IOException("文件已移动到：\n"+folder+"\n\n后续步骤未完成："+ex.Message,ex));}
            finally{organizing=false;}
        }
        void CreateFiveOrganizingCapsules(System.Drawing.Rectangle screen)
        {
            var created=new List<Capsule>();
            var planned=OrganizingCapsules.Plan(Preferences,new Random(Guid.NewGuid().GetHashCode()));
            int nextColor=Preferences.NextColor;
            var positions=GridSnap.RightCluster(screen.Left,screen.Top,screen.Width,screen.Height,88,98);
            try
            {
                for(int i=0;i<planned.Length;i++)
                {
                    var c=planned[i];
                    Preferences.Capsules.Add(c);created.Add(c);CreateWindow(c,true);
                    var position=Windows[c.Id].FromPixels(new System.Windows.Point(positions[i].X,positions[i].Y));
                    var w=Windows[c.Id];w.Left=position.X;w.Top=position.Y;w.ClampPosition();w.Remember();
                    w.Opacity=1;
                }
                Preferences.NextColor=nextColor+5;
                Settings.Save(Preferences);RefreshTray();
                foreach(var c in created)Native.DesktopLayer(Windows[c.Id].Handle,true);
                RefreshDesktopGrid(true);
            }
            catch
            {
                foreach(var c in created)
                {
                    CapsuleWindow window;
                    if(Windows.TryGetValue(c.Id,out window)){window.Close();Windows.Remove(c.Id);}
                    Preferences.Capsules.Remove(c);
                }
                Preferences.NextColor=nextColor;
                throw;
            }
        }
        void PlaceByDesktopIcons(Capsule c,bool nearCursor)
        {
            var w=Windows[c.Id];var area=w.WorkArea(nearCursor);
            var occupied=Windows.Values.Where(other=>other!=w).Select(other=>new GridPoint(other.Left,other.Top));
            var point=GridSnap.SuggestSpawn(area.Left,area.Top,area.Right,area.Bottom,w.Width,w.Height,occupied,DesktopIconGrid.InWindowCoordinates(w));
            w.Left=point.X;w.Top=point.Y;w.ClampPosition();w.Remember();
        }
        public void KeepManualPosition(Capsule c)
        {
            if(Preferences.Capsules.Count>0&&Preferences.Capsules[0]==c)freshPlacementPending=false;
        }
        public void Rename(Capsule c)
        {
            ClosePopup();Dialogs++;string name;
            try{name=Ui.AskName(Lang.Text("胶囊更名","Rename capsule","カプセル名の変更"),c.Name,()=>ChangeIcon(c),()=>RestoreIcon(c),()=>CustomIcons.Source(Settings.DataDirectory,c),
                ()=>ChangeAppearance(c),()=>c.HasCustomIcon,()=>c.IconOpacityPercent);}finally{Dialogs--;}
            if(name==null)return;RenameCapsule(c,name);
        }
        public void RenameQuick(Capsule c)
        {
            ClosePopup();Dialogs++;string name;
            try{name=Ui.AskName(Lang.Text("胶囊更名","Rename capsule","カプセル名の変更"),c.Name);}
            finally{Dialogs--;}
            if(name!=null)RenameCapsule(c,name);
        }
        internal void RenameCapsule(Capsule c,string name)
        {
            c.Name=Logic.CleanName(name);Save();Windows[c.Id].UpdateName();RefreshTray();
        }
        public void ChangeIcon(Capsule c)
        {
            var dialog=new Microsoft.Win32.OpenFileDialog{Title=Lang.Text("为“","Choose an image for ","画像を選択：")+c.Name,Filter=Lang.Text("图标图片","Images","画像")+"|*.png;*.jpg;*.jpeg;*.bmp;*.ico",Multiselect=false,CheckFileExists=true};
            var owner=Application.Current.Windows.OfType<Window>().FirstOrDefault(w=>w.IsActive);
            if(dialog.ShowDialog(owner)!=true)return;
            try
            {
                var image=CustomIcons.Decode(dialog.FileName);
                string detail=Path.GetFileName(dialog.FileName)+"\n"+image.PixelWidth+" × "+image.PixelHeight+Lang.Text(" 像素\n\n确认替换“"," pixels\n\nReplace the image of "," px\n\n画像を変更：")+c.Name+Lang.Text("”的桌面图标？\n图片会缩小保存到便携版 Data/Icons。","?\nA resized copy is saved in Data/Icons.","？\n縮小したコピーを Data/Icons に保存します。");
                if(owner==null?MessageBox.Show(detail,Ui.Product,MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes:MessageBox.Show(owner,detail,Ui.Product,MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
                CustomIcons.Save(Settings.DataDirectory,c,dialog.FileName);
                c.HasCustomIcon=true;Save();Windows[c.Id].UpdateIcon();
            }
            catch(Exception ex){Error(new IOException("图标替换失败："+ex.Message,ex));}
        }
        Capsule[] ActiveSelection(IEnumerable<Capsule> selected)
        {
            return (selected??Enumerable.Empty<Capsule>()).Where(c=>c!=null&&Preferences.Capsules.Contains(c)).Distinct().ToArray();
        }
        public void ChangeIcons(IEnumerable<Capsule> selected)
        {
            var capsules=ActiveSelection(selected);
            if(capsules.Length==0)return;
            var dialog=new Microsoft.Win32.OpenFileDialog{Title=Lang.Text("为选中的胶囊选择统一图标","Choose one image for selected capsules","選択したカプセルの共通画像を選択"),Filter=Lang.Text("图标图片","Images","画像")+"|*.png;*.jpg;*.jpeg;*.bmp;*.ico",Multiselect=false,CheckFileExists=true};
            var owner=Application.Current.Windows.OfType<Window>().FirstOrDefault(w=>w.IsActive);
            if(dialog.ShowDialog(owner)!=true)return;
            try
            {
                var image=CustomIcons.Decode(dialog.FileName);
                string detail=Path.GetFileName(dialog.FileName)+"\n"+image.PixelWidth+" × "+image.PixelHeight+Lang.Text(" 像素\n\n确认给选中的 "," pixels\n\nUse this image for "," px\n\nこの画像を ")+capsules.Length+Lang.Text(" 个胶囊统一使用这张图片？\n每个胶囊会在 Data/Icons 中保存自己的副本。"," selected capsules?\nEach gets its own copy in Data/Icons."," 個のカプセルに適用しますか？\n各カプセルに Data/Icons 内のコピーを保存します。");
                var choice=owner==null?MessageBox.Show(detail,Ui.Product,MessageBoxButton.YesNo,MessageBoxImage.Question):
                    MessageBox.Show(owner,detail,Ui.Product,MessageBoxButton.YesNo,MessageBoxImage.Question);
                if(choice!=MessageBoxResult.Yes)return;
                int changed=0;var failed=new List<string>();
                foreach(var c in capsules)
                {
                    try{CustomIcons.SaveImage(Settings.DataDirectory,c,image);c.HasCustomIcon=true;Windows[c.Id].UpdateIcon();changed++;}
                    catch(Exception ex){failed.Add(c.Name+"："+ex.Message);}
                }
                if(changed>0){ClosePopup();Settings.Save(Preferences);}
                if(failed.Count>0)Error(new IOException(Lang.Text("已更新 ","Updated ","更新済み ")+changed+Lang.Text(" 个胶囊；以下胶囊未完成：\n"," capsules; these failed:\n"," 個。以下は失敗：\n")+String.Join("\n",failed.Take(8))));
            }
            catch(Exception ex){Error(new IOException("批量替换图标失败："+ex.Message,ex));}
        }
        public void RestoreIcon(Capsule c)
        {
            try { CustomIcons.Remove(Settings.DataDirectory,c);c.HasCustomIcon=false;Save();Windows[c.Id].UpdateIcon(); }
            catch(Exception ex){Error(new IOException("还原默认图标失败："+ex.Message,ex));}
        }
        public void ChangeAppearance(Capsule c)
        {
            if(c.HasCustomIcon){MessageBox.Show(Lang.Text("请先还原默认胶囊图，再更换颜色和透明度。","Restore the default image before changing color or opacity.","色や透明度を変更する前に既定の画像へ戻してください。"),Ui.Product);return;}
            Dialogs++;
            try
            {
                var dialog=new CapsuleAppearanceDialog(c);
                if(dialog.ShowDialog()!=true)return;
                int oldColor=c.ColorIndex,oldOpacity=c.IconOpacityPercent;
                c.ColorIndex=dialog.SelectedColor;
                c.IconOpacityPercent=dialog.SelectedOpacity;
                try{Settings.Save(Preferences);}
                catch(Exception ex){c.ColorIndex=oldColor;c.IconOpacityPercent=oldOpacity;Error(new IOException("胶囊外观未能保存："+ex.Message,ex));return;}
                Windows[c.Id].UpdateIcon();
                if(Popup!=null&&Popup.Capsule.Id==c.Id)ClosePopup();
            }
            finally{Dialogs--;}
        }
        public void ChangeColor(Capsule c)
        {
            if(c.HasCustomIcon)
            {
                MessageBox.Show(Lang.Text("这个胶囊使用自定义图片。请先在胶囊管理中还原默认胶囊图，再换色。",
                    "This capsule uses a custom image. Restore the default image in Capsule Manager before changing its color.",
                    "このカプセルはカスタム画像を使用しています。色を変える前に管理画面で既定画像に戻してください。"),Ui.Product);
                return;
            }
            ClosePopup();Dialogs++;
            try
            {
                var dialog=new CapsuleAppearanceDialog(c,AppearanceEditMode.ColorOnly);
                if(dialog.ShowDialog()!=true)return;
                int old=c.ColorIndex;c.ColorIndex=dialog.SelectedColor;
                try{Settings.Save(Preferences);}
                catch(Exception ex){c.ColorIndex=old;Error(new IOException("胶囊颜色未能保存："+ex.Message,ex));return;}
                Windows[c.Id].UpdateIcon();
            }
            finally{Dialogs--;}
        }
        public void ChangeOpacity(Capsule c)
        {
            ClosePopup();Dialogs++;
            try
            {
                var dialog=new CapsuleAppearanceDialog(c,AppearanceEditMode.OpacityOnly,CustomIcons.Source(Settings.DataDirectory,c));
                if(dialog.ShowDialog()!=true)return;
                int old=c.IconOpacityPercent;c.IconOpacityPercent=dialog.SelectedOpacity;
                try{Settings.Save(Preferences);}
                catch(Exception ex){c.IconOpacityPercent=old;Error(new IOException("胶囊透明度未能保存："+ex.Message,ex));return;}
                Windows[c.Id].UpdateIcon();
            }
            finally{Dialogs--;}
        }
        public void ChangeAppearances(IEnumerable<Capsule> selected)
        {
            var capsules=ActiveSelection(selected);
            var eligible=capsules.Where(c=>!c.HasCustomIcon).ToArray();
            if(eligible.Length==0){MessageBox.Show(Lang.Text("所选胶囊均使用自定义图片。请先还原默认胶囊图，再统一换色。","All selected capsules use custom images. Restore their default images before changing colors.","選択したカプセルはすべてカスタム画像です。色を変える前に既定画像へ戻してください。"),Ui.Product);return;}
            var dialog=new CapsuleAppearanceDialog(eligible[0]);
            if(dialog.ShowDialog()!=true)return;
            var old=eligible.Select(c=>Tuple.Create(c,c.ColorIndex,c.IconOpacityPercent)).ToArray();
            foreach(var c in eligible){c.ColorIndex=dialog.SelectedColor;c.IconOpacityPercent=dialog.SelectedOpacity;}
            try{Settings.Save(Preferences);}
            catch(Exception ex)
            {
                foreach(var item in old){item.Item1.ColorIndex=item.Item2;item.Item1.IconOpacityPercent=item.Item3;}
                Error(new IOException("批量外观未能保存："+ex.Message,ex));return;
            }
            foreach(var c in eligible)Windows[c.Id].UpdateIcon();
            ClosePopup();
            int skipped=capsules.Length-eligible.Length;
            if(skipped>0)MessageBox.Show(Lang.Text("已统一调整 ","Updated ","変更済み ")+eligible.Length+Lang.Text(" 个胶囊；另有 "," capsules; "," 個。")+skipped+Lang.Text(" 个使用自定义图片，未换色。"," with custom images were skipped."," 個はカスタム画像のため対象外です。"),Ui.Product);
        }
        public void DeleteCapsules(IEnumerable<Capsule> selected)
        {
            var capsules=ActiveSelection(selected);
            if(capsules.Length==0)return;
            string detail=Lang.Text("确定删除选中的 ","Delete ","選択した ")+capsules.Length+Lang.Text(" 个胶囊？\n\n胶囊内的文件副本会移入回收站；旧版路径引用只从胶囊移除，原文件不受影响。",
                " selected capsules?\n\nStored copies go to the Recycle Bin. Legacy path links are removed from the capsule only; original files stay untouched.",
                " 個のカプセルを削除しますか？\n\n保存済みコピーはごみ箱へ移動します。旧版の参照はカプセルからのみ削除され、元ファイルは残ります。");
            var owner=Application.Current.Windows.OfType<Window>().FirstOrDefault(w=>w.IsActive);
            var choice=owner==null?MessageBox.Show(detail,Ui.Product,MessageBoxButton.YesNo,MessageBoxImage.Question):
                MessageBox.Show(owner,detail,Ui.Product,MessageBoxButton.YesNo,MessageBoxImage.Question);
            if(choice!=MessageBoxResult.Yes)return;
            int deleted=0;var failed=new List<string>();
            foreach(var c in capsules)
            {
                try{RemoveCapsule(c);deleted++;}
                catch(Exception ex){failed.Add(c.Name+"："+ex.Message);}
            }
            if(failed.Count>0)Error(new IOException("已删除 "+deleted+" 个胶囊；以下胶囊未完成：\n"+String.Join("\n",failed.Take(8))));
        }
        public void DeleteCapsule(Capsule c)
        {
            ClosePopup();
            if(MessageBox.Show(Lang.Text("删除胶囊“","Delete capsule ","カプセルを削除：")+c.Name+Lang.Text("”？\n胶囊内的文件副本会移入回收站；旧版路径引用只从胶囊移除，原文件不受影响。",
                "?\nStored copies go to the Recycle Bin. Legacy links are removed from the capsule only; original files remain.",
                "？\n保存済みコピーはごみ箱へ移動します。旧版の参照はカプセルからのみ削除され、元ファイルは残ります。"),Ui.Product,MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            try
            {
                RemoveCapsule(c);
            }
            catch(Exception ex){Error(ex);}
        }
        internal void RemoveCapsule(Capsule c)
        {
            ClosePopup();Files.RemoveCapsule(c,RuntimeMode.Recycle);CustomIcons.Remove(Settings.DataDirectory,c);
            Preferences.Capsules.Remove(c);Windows[c.Id].Close();Windows.Remove(c.Id);Save();RefreshTray();
        }
        internal void SetTaskbarMode(string mode)
        {
            if(!new[]{"normal","clear","acrylic"}.Contains(mode))throw new ArgumentException("mode");
            Preferences.TaskbarMode=mode;Save();ApplyTaskbar(false);RefreshTray();
        }
        public bool Add(Capsule c,IEnumerable<string> paths)
        {
            int previousCount=c.Items.Count;
            var failures=new List<string>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var path in paths)
            {
                if(!seen.Add(path))continue;
                try{Files.Add(c,path);}catch(Exception ex){failures.Add(Path.GetFileName(path)+"："+ex.Message);}
            }
            bool saved=true;
            try{Settings.Save(Preferences);}catch(Exception ex){saved=false;failures.Add(Lang.Text("配置保存失败：","Could not save configuration: ","設定の保存に失敗：")+ex.Message);}
            if(Popup!=null&&Popup.Capsule.Id==c.Id)Popup.Refresh(c.Items.Count>previousCount);
            if(failures.Count>0)Error(new IOException(String.Join("\n",failures.Take(8))));
            return saved&&failures.Count==0&&seen.Count>0;
        }
        public void ChooseFiles(Capsule c)
        {
            Dialogs++;
            try
            {
                var dialog=new Microsoft.Win32.OpenFileDialog{Title=Lang.Text("添加文件到“","Add files to ","ファイルを追加：")+c.Name+Lang.Text("”","",""),Multiselect=true,DereferenceLinks=false,CheckFileExists=true,Filter=Lang.Text("所有文件","All files","すべてのファイル")+"|*.*"};
                if(dialog.ShowDialog()==true)Add(c,dialog.FileNames);
            }
            finally{Dialogs--;if(Popup!=null){Popup.Activate();Popup.Focus();}}
        }
        public void DeleteEntries(Capsule c,IEnumerable<Shortcut> entries)
        {
            try{Files.Remove(c,entries,RuntimeMode.Recycle);}catch(Exception ex){Error(ex);}
            finally{Save();if(Popup!=null&&Popup.Capsule.Id==c.Id)Popup.Refresh();}
        }
        public void MoveEntriesOut(Capsule c,IEnumerable<Shortcut> entries)
        {
            try{Files.RemoveTransferred(c,entries,RuntimeMode.Recycle);}
            catch(Exception ex){Error(new IOException("拖出后未能清理胶囊条目，请检查目标文件和胶囊内容："+ex.Message,ex));}
            finally{Save();if(Popup!=null&&Popup.Capsule.Id==c.Id)Popup.Refresh();}
        }
        public void Launch(Capsule c,Shortcut entry)
        {
            var path=Files.EntryPath(c,entry);ClosePopup();
            if(RuntimeMode.Sandbox){RuntimeMode.Log("LAUNCH simulated "+path);return;}
            try
            {
                if(!File.Exists(path)&&!Directory.Exists(path))throw new IOException(entry.IsReference?"原文件已移动、删除或暂时不可访问：\n"+path+"\n\n请恢复原路径，或移除此条目后重新拖入。":"胶囊内文件副本丢失：\n"+path);
                Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
            }
            catch(Exception ex){Error(ex);}
        }
        public bool DisableStartupOnly()
        {
            if(disposed)return false;
            bool previous=Preferences.StartWithWindows;
            try
            {
                Settings.SetStartup(false);
                Preferences.StartWithWindows=false;
                Settings.Save(Preferences);
                RefreshTray();
                return true;
            }
            catch(Exception ex)
            {
                Preferences.StartWithWindows=previous;
                if(previous)try{Settings.SetStartup(true);}catch(Exception restore){StartupLog.Write("startup rollback failed "+restore);}
                Error(new IOException("未能关闭开机自启，程序仍在运行："+ex.Message,ex));
                return false;
            }
        }
        public void Exit(){if(disposed)return;if(organizing){Notify("桌面整理仍在进行，完成后再退出。");return;}ClosePopup();Save();Dispose();Application.Current.Shutdown();}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(foregroundHook!=IntPtr.Zero)Native.UnhookWinEvent(foregroundHook);
            if(minimizeHook!=IntPtr.Zero)Native.UnhookWinEvent(minimizeHook);
            if(hideHook!=IntPtr.Zero)Native.UnhookWinEvent(hideHook);
            desktopRecovery.Stop();
            desktopGridTimer.Stop();
            shellRecovery.Stop();
            copyMigration.Stop();
            ClosePopup();if(quitWait!=null)quitWait.Unregister(null);if(quitEvent!=null)quitEvent.Dispose();
            if(taskbar!=null)taskbar.Dispose();
            Icons.Dispose();
            var icon=tray.Icon;tray.Visible=false;tray.ContextMenuStrip.Dispose();tray.Dispose();if(icon!=null)icon.Dispose();
            foreach(var w in Windows.Values.ToArray())w.Close();Controller.Dispose();
        }
    }
}


