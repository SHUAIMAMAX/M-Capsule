using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
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
        readonly DispatcherTimer shellRecovery;
        int shellAttempts;
        readonly Native.MouseHook outside;
        readonly EventWaitHandle quitEvent;
        readonly RegisteredWaitHandle quitWait;
        IntPtr foregroundHook, minimizeHook, hideHook, mouseHook;
        int recoveryTicks;
        TaskbarController taskbar;
        bool disposed;
        int taskbarErrorPending;
        DateTime lastGridRefresh=DateTime.MinValue;
        public Manager()
        {
            Preferences=Settings.Load();
            Icons=new IconCache(Application.Current.Dispatcher);
            Directory.CreateDirectory(Settings.DataDirectory);
            Controller=new HwndSource(new HwndSourceParameters(RuntimeMode.ControllerTitle) { Width=1,Height=1,WindowStyle=0 });
            Controller.AddHook(Messages);
            foreground = OnForeground;
            desktopChange=OnDesktopChange;
            desktopRecovery=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(100)};
            desktopRecovery.Tick+=(s,e)=>RecoverDesktop();
            shellRecovery=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(500)};
            shellRecovery.Tick+=(s,e)=>
            {
                bool ready=Windows.Count>0;
                foreach(var w in Windows.Values)
                    if(!w.Interacting&&!Native.DesktopLayer(w.Handle,true))ready=false;
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
        void CreateWindow(Capsule c)
        {
            var w=new CapsuleWindow(this,c);
            if(Windows.Count==0)w.ContentRendered+=(s,e)=>{Program.LogFirstFrame();RefreshDesktopGrid();};
            Windows[c.Id]=w;w.Show();
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
            m.Items.Add("在桌面新建胶囊…",null,(s,e)=>NewCapsule());
            var startup=new Forms.ToolStripMenuItem("开机自动启动") { Checked=Preferences.StartWithWindows };
            startup.Click+=(s,e)=>{try{bool enabled=!Preferences.StartWithWindows;Settings.SetStartup(enabled);Preferences.StartWithWindows=enabled;Save();startup.Checked=enabled;}catch(Exception ex){Error(ex);}};
            m.Items.Add(startup);
            m.Items.Add("检查/修复开机自启…",null,(s,e)=>
            {
                try
                {
                    Settings.SetStartup(true);Preferences.StartWithWindows=true;Save();RefreshTray();
                    MessageBox.Show("已创建并读回核验登录自启快捷方式：\n"+Settings.StartupLink+"\n\n目标："+Settings.Executable+"\n配置："+Settings.DataDirectory+"\n\n如果 Windows 设置中禁用了此启动项，请在‘应用 → 启动’中启用。启动过程记录在 Data\\Logs\\startup.log。",Ui.Product);
                }
                catch(Exception ex){Error(ex);}
            });
            var appearance=new Forms.ToolStripMenuItem("任务栏外观");
            foreach(var pair in new[]{new[]{"normal","正常"},new[]{"acrylic","毛玻璃"},new[]{"clear","全透明"}})
            {
                string mode=pair[0];var item=new Forms.ToolStripMenuItem(pair[1]){Checked=Preferences.TaskbarMode==mode};
                item.Click+=(s,e)=>{SetTaskbarMode(mode);};appearance.DropDownItems.Add(item);
            }
            m.Items.Add(appearance);
            m.Items.Add("更名或删除胶囊…",null,(s,e)=>ShowCapsuleList());
            m.Items.Add(new Forms.ToolStripSeparator());
            var open=new Forms.ToolStripMenuItem("打开胶囊");
            foreach(var capsule in Preferences.Capsules){var c=capsule;open.DropDownItems.Add(c.Name,null,(s,e)=>Open(c));}
            open.Enabled=Preferences.Capsules.Count>0;m.Items.Add(open);
            m.Items.Add("使用说明",null,(s,e)=>{try{Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"使用说明.txt")){UseShellExecute=true});}catch(Exception ex){Error(ex);}});
            m.Items.Add(new Forms.ToolStripSeparator());
            m.Items.Add("退出",null,(s,e)=>Exit());
            return m;
        }
        public void RefreshTray()
        {
            var old=tray.ContextMenuStrip;tray.ContextMenuStrip=BuildTray();
            if(old!=null)Application.Current.Dispatcher.BeginInvoke(new Action(()=>old.Dispose()),DispatcherPriority.Background);
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
                foreach(var w in Windows.Values)if(!w.Interacting)Native.DesktopLayer(w.Handle,desktop);
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
                if(!w.Interacting && Native.IsWindow(w.Handle))Native.DesktopLayer(w.Handle,true);
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
        public void Reposition(){ClosePopup();foreach(var c in Preferences.Capsules){var w=Windows[c.Id];w.ClampPosition();Native.DesktopLayer(w.Handle,Native.DesktopForeground());}Save();}
        void RefreshDesktopGrid()
        {
            if(RuntimeMode.Sandbox||disposed||(DateTime.UtcNow-lastGridRefresh).TotalSeconds<5)return;
            lastGridRefresh=DateTime.UtcNow;
            DesktopIconGrid.Refresh(()=>{if(!disposed)Reposition();});
        }
        public void ShowCapsuleList()
        {
            ClosePopup();Dialogs++;
            try{new CapsuleListDialog(this).ShowDialog();}finally{Dialogs--;}
        }
        public void NewCapsule()
        {
            if(Preferences.Capsules.Count>=Logic.MaxCapsules){Notify("最多创建 32 个胶囊。");return;}
            ClosePopup();Dialogs++;
            string name;try{name=Ui.AskName("新建胶囊","小工具");}finally{Dialogs--;}
            if(name==null)return;
            CreateCapsule(name);
        }
        internal Capsule CreateCapsule(string name)
        {
            if(Preferences.Capsules.Count>=Logic.MaxCapsules)throw new IOException("最多创建 32 个胶囊。");
            var c=new Capsule{Name=Logic.CleanName(name),ColorIndex=Preferences.NextColor++,X=32+Preferences.Capsules.Count%6*92,Y=140+Preferences.Capsules.Count/6*110};
            Preferences.Capsules.Add(c);Save();CreateWindow(c);Native.DesktopLayer(Windows[c.Id].Handle,true);RefreshTray();return c;
        }
        public void Rename(Capsule c)
        {
            ClosePopup();Dialogs++;string name;
            try{name=Ui.AskName("胶囊更名",c.Name,()=>ChangeIcon(c),()=>RestoreIcon(c),()=>CustomIcons.Source(Settings.DataDirectory,c));}finally{Dialogs--;}
            if(name==null)return;RenameCapsule(c,name);
        }
        internal void RenameCapsule(Capsule c,string name)
        {
            c.Name=Logic.CleanName(name);Save();Windows[c.Id].UpdateName();RefreshTray();
        }
        public void ChangeIcon(Capsule c)
        {
            var dialog=new Microsoft.Win32.OpenFileDialog{Title="为“"+c.Name+"”选择图标图片",Filter="图标图片|*.png;*.jpg;*.jpeg;*.bmp;*.ico",Multiselect=false,CheckFileExists=true};
            var owner=Application.Current.Windows.OfType<Window>().FirstOrDefault(w=>w.IsActive);
            if(dialog.ShowDialog(owner)!=true)return;
            try
            {
                var image=CustomIcons.Decode(dialog.FileName);
                string detail=Path.GetFileName(dialog.FileName)+"\n"+image.PixelWidth+" × "+image.PixelHeight+" 像素\n\n确认替换“"+c.Name+"”的桌面图标？\n图片会缩小保存到便携版 Data/Icons。";
                if(owner==null?MessageBox.Show(detail,Ui.Product,MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes:MessageBox.Show(owner,detail,Ui.Product,MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
                CustomIcons.Save(Settings.DataDirectory,c,dialog.FileName);
                c.HasCustomIcon=true;Save();Windows[c.Id].UpdateIcon();
            }
            catch(Exception ex){Error(new IOException("图标替换失败："+ex.Message,ex));}
        }
        public void RestoreIcon(Capsule c)
        {
            try { CustomIcons.Remove(Settings.DataDirectory,c);c.HasCustomIcon=false;Save();Windows[c.Id].UpdateIcon(); }
            catch(Exception ex){Error(new IOException("还原默认图标失败："+ex.Message,ex));}
        }
        public void DeleteCapsule(Capsule c)
        {
            ClosePopup();
            if(MessageBox.Show("删除胶囊“"+c.Name+"”？\n仅移除文件引用；旧版快捷方式副本移入回收站。原文件、文件夹和软件不受影响。",Ui.Product,MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
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
        public void Add(Capsule c,IEnumerable<string> paths)
        {
            int previousCount=c.Items.Count;
            var failures=new List<string>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var path in paths)
            {
                if(!seen.Add(path))continue;
                try{Files.AddReference(c,path);}catch(Exception ex){failures.Add(Path.GetFileName(path)+"："+ex.Message);}
            }
            Save();if(Popup!=null&&Popup.Capsule.Id==c.Id)Popup.Refresh(c.Items.Count>previousCount);
            if(failures.Count>0)Error(new IOException(String.Join("\n",failures.Take(8))));
        }
        public void ChooseFiles(Capsule c)
        {
            Dialogs++;
            try
            {
                var dialog=new Microsoft.Win32.OpenFileDialog{Title="添加文件引用到“"+c.Name+"”",Multiselect=true,DereferenceLinks=false,CheckFileExists=true,Filter="所有文件|*.*"};
                if(dialog.ShowDialog()==true)Add(c,dialog.FileNames);
            }
            finally{Dialogs--;if(Popup!=null){Popup.Activate();Popup.Focus();}}
        }
        public void DeleteEntries(Capsule c,IEnumerable<Shortcut> entries)
        {
            try{Files.Remove(c,entries,RuntimeMode.Recycle);}catch(Exception ex){Error(ex);}
            finally{Save();if(Popup!=null&&Popup.Capsule.Id==c.Id)Popup.Refresh();}
        }
        public void Launch(Capsule c,Shortcut entry)
        {
            var path=Files.EntryPath(c,entry);ClosePopup();
            if(RuntimeMode.Sandbox){RuntimeMode.Log("LAUNCH simulated "+path);return;}
            try
            {
                if(!File.Exists(path)&&!Directory.Exists(path))throw new IOException("原文件已移动、删除或暂时不可访问：\n"+path+"\n\n胶囊只保存引用。恢复原路径，或移除此条目后重新拖入。");
                Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
            }
            catch(Exception ex){Error(ex);}
        }
        public void Exit(){if(disposed)return;ClosePopup();Save();Dispose();Application.Current.Shutdown();}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(foregroundHook!=IntPtr.Zero)Native.UnhookWinEvent(foregroundHook);
            if(minimizeHook!=IntPtr.Zero)Native.UnhookWinEvent(minimizeHook);
            if(hideHook!=IntPtr.Zero)Native.UnhookWinEvent(hideHook);
            desktopRecovery.Stop();
            shellRecovery.Stop();
            ClosePopup();if(quitWait!=null)quitWait.Unregister(null);if(quitEvent!=null)quitEvent.Dispose();
            if(taskbar!=null)taskbar.Dispose();
            Icons.Dispose();
            var icon=tray.Icon;tray.Visible=false;tray.ContextMenuStrip.Dispose();tray.Dispose();if(icon!=null)icon.Dispose();
            foreach(var w in Windows.Values.ToArray())w.Close();Controller.Dispose();
        }
    }
}


