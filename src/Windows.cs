using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace PocketGrid
{
    internal sealed class CapsuleWindow : Window
    {
        readonly Manager manager;
        public readonly Capsule Capsule;
        readonly TextBlock label;
        readonly Image icon;
        readonly Border frame;
        readonly ContextMenu menu;
        readonly DispatcherTimer hoverTimer;
        bool pressed,moving;
        double initialX,initialY;
        System.Drawing.Point initialCursor;
        public IntPtr Handle { get; private set; }
        public bool Interacting { get{return pressed||menu.IsOpen;} }
        public CapsuleWindow(Manager manager,Capsule capsule)
        {
            this.manager=manager;Capsule=capsule;
            Title="M-Capsule."+capsule.Id;
            Width=88;Height=98;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=false;
            Left=Logic.Clamp(capsule.X,-100000,100000);Top=Logic.Clamp(capsule.Y,-100000,100000);AllowDrop=true;UseLayoutRounding=true;
            var root=new Grid{Background=Brushes.Transparent,Cursor=Cursors.Hand};
            root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(70)});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(28)});
            frame=new Border{CornerRadius=new CornerRadius(15),Background=Brushes.Transparent,Margin=new Thickness(6,0,6,0)};
            icon=new Image{Source=CustomIcons.Source(Settings.DataDirectory,capsule),Width=64,Height=64,Stretch=Stretch.Uniform,
                Opacity=capsule.HasCustomIcon?1:capsule.IconOpacityPercent/100.0};
            frame.Child=icon;root.Children.Add(frame);
            label=Ui.Text(capsule.Name,12,Brushes.White);label.HorizontalAlignment=HorizontalAlignment.Center;label.MaxWidth=86;label.TextAlignment=TextAlignment.Center;
            label.Effect=new DropShadowEffect{BlurRadius=4,ShadowDepth=1,Opacity=.9,Color=Colors.Black};Grid.SetRow(label,1);root.Children.Add(label);
            Content=root;
            hoverTimer=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(500)};
            hoverTimer.Tick+=(s,e)=>
            {
                hoverTimer.Stop();
                if(pressed||moving||!IsVisible||!Native.PointerOver(Handle))return;
                if(manager.Popup==null||manager.Popup.Capsule.Id!=Capsule.Id)manager.Open(Capsule);
            };
            menu=new ContextMenu();
            AddMenu("添加文件…",()=>manager.ChooseFiles(capsule));
            AddMenu("更名或删除胶囊…",()=>manager.ShowCapsuleList());ContextMenu=menu;
            UpdateLanguage();
            root.MouseEnter+=(s,e)=>{frame.Background=Ui.Brush(25,255,255,255);if(!pressed){hoverTimer.Stop();hoverTimer.Start();}};
            root.MouseLeave+=(s,e)=>{frame.Background=Brushes.Transparent;hoverTimer.Stop();};
            root.MouseLeftButtonDown+=(s,e)=>
            {
                hoverTimer.Stop();manager.KeepManualPosition(Capsule);pressed=true;moving=false;initialCursor=Forms.Cursor.Position;initialX=Left;initialY=Top;root.CaptureMouse();e.Handled=true;
            };
            root.MouseMove+=(s,e)=>
            {
                if(!pressed)return;var now=Forms.Cursor.Position;
                var delta=FromPixels(new System.Windows.Point(now.X-initialCursor.X,now.Y-initialCursor.Y));
                if(Math.Abs(delta.X)+Math.Abs(delta.Y)<6)return;
                moving=true;manager.ClosePopup();Left=initialX+delta.X;Top=initialY+delta.Y;
            };
            root.MouseLeftButtonUp+=(s,e)=>
            {
                if(!pressed)return;pressed=false;root.ReleaseMouseCapture();
                if(moving){ClampPosition();manager.Save();}else manager.Open(Capsule);
                e.Handled=true;
            };
            root.LostMouseCapture+=(s,e)=>pressed=false;
            DragOver+=(s,e)=>
            {
                OnDragOver(s,e);
                if(String.Equals(e.Data.GetData("M-Capsule.InternalDrag") as string,Capsule.Id,StringComparison.OrdinalIgnoreCase))e.Effects=DragDropEffects.None;
                if(e.Data.GetDataPresent(DataFormats.FileDrop))manager.Open(Capsule,true);
            };
            Drop+=(s,e)=>
            {
                if(!e.Data.GetDataPresent(DataFormats.FileDrop)||
                   String.Equals(e.Data.GetData("M-Capsule.InternalDrag") as string,Capsule.Id,StringComparison.OrdinalIgnoreCase))return;
                bool accepted=manager.Add(Capsule,(string[])e.Data.GetData(DataFormats.FileDrop));
                if(manager.Popup==null||manager.Popup.Capsule.Id!=Capsule.Id)manager.Open(Capsule);
                if(manager.Popup!=null)manager.Popup.AcceptDrop();
                e.Effects=accepted?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;
            };
            SourceInitialized+=(s,e)=>
            {
                Handle=new WindowInteropHelper(this).Handle;Native.StyleAnchor(Handle);
                HwndSource.FromHwnd(Handle).AddHook((IntPtr h,int m,IntPtr w,IntPtr l,ref bool handled)=>{if(m==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;});
                ClampPosition();
            };
            Loaded+=(s,e)=>Native.DesktopLayer(Handle,Native.DesktopForeground());
            Closed+=(s,e)=>hoverTimer.Stop();
        }
        void AddMenu(string title,Action action){var item=new MenuItem{Header=title};item.Click+=(s,e)=>action();menu.Items.Add(item);}
        public void UpdateLanguage()
        {
            ToolTip=Lang.Text("悬停半秒展开 · 拖动后自动对齐桌面网格\n拖入文件保存副本；文件夹保存路径引用",
                "Hover 0.5 s to open · Drag to align with the desktop grid\nDropped files are copied; folders remain linked",
                "0.5 秒ホバーで開く · ドラッグでグリッドに整列\n追加したファイルはコピー、フォルダーは参照");
            ((MenuItem)menu.Items[0]).Header=Lang.Text("添加文件…","Add files…","ファイルを追加…");
            ((MenuItem)menu.Items[1]).Header=Lang.Text("胶囊管理…","Capsule Manager…","カプセル管理…");
        }
        public static void OnDragOver(object sender,DragEventArgs e)
        {
            e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;
        }
        public System.Windows.Point FromPixels(System.Windows.Point p)
        {
            var source=PresentationSource.FromVisual(this);
            return source==null||source.CompositionTarget==null?p:source.CompositionTarget.TransformFromDevice.Transform(p);
        }
        public Rect WorkArea(bool cursor=false)
        {
            var area=(cursor?Forms.Screen.FromPoint(Forms.Cursor.Position):Forms.Screen.FromHandle(Handle)).WorkingArea;
            return new Rect(FromPixels(new System.Windows.Point(area.Left,area.Top)),FromPixels(new System.Windows.Point(area.Right,area.Bottom)));
        }
        public void ClampPosition()
        {
            var w=WorkArea();
            var occupied=manager.Windows.Values.Where(other=>other!=this)
                .Select(other=>new GridPoint(other.Left,other.Top));
            var point=GridSnap.PlaceOnDesktop(Left,Top,w.Left,w.Top,w.Right,w.Bottom,Width,Height,occupied,DesktopIconGrid.InWindowCoordinates(this));
            Left=point.X;Top=point.Y;Remember();
        }
        public void Remember(){Capsule.X=Left;Capsule.Y=Top;}
        public void UpdateIconOcclusion()
        {
            // A dense desktop must never make a capsule disappear behind Explorer icons.
            if(!IsVisible)
            {
                Show();Native.DesktopLayer(Handle,true);
            }
        }
        public void UpdateName(){label.Text=Capsule.Name;}
        public void UpdateIcon(){icon.Source=CustomIcons.Source(Settings.DataDirectory,Capsule);icon.Opacity=Capsule.HasCustomIcon?1:Capsule.IconOpacityPercent/100.0;}
    }

    internal sealed class GridWindow : Window
    {
        readonly Manager manager;
        readonly CapsuleWindow anchor;
        readonly Grid content;
        readonly ListBox list;
        readonly TextBlock count,empty;
        readonly Button previousPage,nextPage;
        readonly Image backdrop;
        readonly Grid surface;
        readonly DispatcherTimer backdropRefresh;
        int backdropVersion;
        int pageIndex,pageSize,pageCount;
        int renderVersion;
        bool closed;
        Point dragStart;
        Shortcut dragEntry;
        public bool DragHover { get; set; }
        public readonly Capsule Capsule;
        public bool MenuOpen { get; private set; }
        public IntPtr Handle{get;private set;}
        public GridWindow(Manager manager,Capsule capsule,CapsuleWindow anchor)
        {
            this.manager=manager;Capsule=capsule;this.anchor=anchor;
            Title=capsule.Name;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;Topmost=true;UseLayoutRounding=true;AllowDrop=true;
            content=new Grid{Margin=new Thickness(20,14,20,12)};
            content.RowDefinitions.Add(new RowDefinition{Height=new GridLength(40)});
            content.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
            content.RowDefinitions.Add(new RowDefinition{Height=new GridLength(18)});
            surface=new Grid{Background=Brushes.Transparent};
            backdrop=new Image{Stretch=Stretch.Fill,IsHitTestVisible=false};surface.Children.Add(backdrop);
            surface.Children.Add(new Border{Background=Ui.Brush(96,25,29,37),IsHitTestVisible=false});
            surface.Children.Add(content);
            var border=new Border{Child=surface,CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1),BorderBrush=Ui.Brush(65,255,255,255)};Content=border;
            backdropRefresh=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(40)};
            backdropRefresh.Tick+=(s,e)=>{backdropRefresh.Stop();RenderBackdrop();};
            var header=new Grid();header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(30)});header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(32)});
            header.Children.Add(new Image{Source=CustomIcons.Source(Settings.DataDirectory,capsule),Width=26,Height=26,
                Opacity=capsule.HasCustomIcon?1:capsule.IconOpacityPercent/100.0});
            var heading=Ui.Text(capsule.Name,15,Ui.White);heading.FontWeight=FontWeights.SemiBold;heading.VerticalAlignment=VerticalAlignment.Center;heading.Margin=new Thickness(7,0,8,0);Grid.SetColumn(heading,1);header.Children.Add(heading);
            var add=Ui.Button("+",Lang.Text("一次添加多个文件；拖入的文件保存副本","Add multiple files; imported files are copied","複数のファイルを追加。取り込んだファイルはコピーされます"));add.Width=30;add.Height=30;add.Click+=(s,e)=>manager.ChooseFiles(Capsule);Grid.SetColumn(add,2);header.Children.Add(add);content.Children.Add(header);
            list=new ListBox{Background=Brushes.Transparent,BorderThickness=new Thickness(0),SelectionMode=SelectionMode.Extended,Padding=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Center,Foreground=Ui.White,FocusVisualStyle=null};
            ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);ScrollViewer.SetVerticalScrollBarVisibility(list,ScrollBarVisibility.Disabled);ScrollViewer.SetCanContentScroll(list,false);
            list.ItemContainerStyle=ItemStyle();Grid.SetRow(list,1);content.Children.Add(list);
            empty=Ui.Text(Lang.Text("把文件或文件夹拖进来\n或点右上角 ＋","Drop a file or folder here\nor click +","ファイルやフォルダーをドラッグ\nまたは ＋ をクリック"),13,Ui.Muted);empty.TextAlignment=TextAlignment.Center;empty.VerticalAlignment=VerticalAlignment.Center;Grid.SetRow(empty,1);content.Children.Add(empty);
            var footer=new Grid();footer.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            count=Ui.Text("",10,Ui.Muted);count.VerticalAlignment=VerticalAlignment.Bottom;footer.Children.Add(count);
            previousPage=Ui.Button("‹",Lang.Text("上一页","Previous page","前のページ"));previousPage.Width=24;previousPage.Height=18;previousPage.FontSize=14;previousPage.Click+=(s,e)=>ChangePage(-1);Grid.SetColumn(previousPage,1);footer.Children.Add(previousPage);
            nextPage=Ui.Button("›",Lang.Text("下一页","Next page","次のページ"));nextPage.Width=24;nextPage.Height=18;nextPage.FontSize=14;nextPage.Click+=(s,e)=>ChangePage(1);Grid.SetColumn(nextPage,2);footer.Children.Add(nextPage);
            Grid.SetRow(footer,2);content.Children.Add(footer);
            list.MouseDoubleClick+=(s,e)=>
            {
                var cell=ItemsControl.ContainerFromElement(list,e.OriginalSource as DependencyObject) as ListBoxItem;
                if(cell!=null&&cell.Tag is Shortcut){e.Handled=true;manager.Launch(Capsule,(Shortcut)cell.Tag);}
            };
            list.PreviewMouseRightButtonDown+=(s,e)=>
            {
                var cell=ItemsControl.ContainerFromElement(list,e.OriginalSource as DependencyObject) as ListBoxItem;
                if(cell!=null&&!cell.IsSelected){list.SelectedItems.Clear();cell.IsSelected=true;}
            };
            list.PreviewMouseLeftButtonDown+=(s,e)=>
            {
                dragStart=e.GetPosition(list);
                var cell=ItemsControl.ContainerFromElement(list,e.OriginalSource as DependencyObject) as ListBoxItem;
                dragEntry=cell==null?null:cell.Tag as Shortcut;
            };
            list.PreviewMouseMove+=(s,e)=>
            {
                if(dragEntry==null||e.LeftButton!=MouseButtonState.Pressed)return;
                var now=e.GetPosition(list);
                if(Math.Abs(now.X-dragStart.X)<SystemParameters.MinimumHorizontalDragDistance&&
                   Math.Abs(now.Y-dragStart.Y)<SystemParameters.MinimumVerticalDragDistance)return;
                var selected=list.SelectedItems.Cast<ListBoxItem>().Select(cell=>cell.Tag as Shortcut).Where(item=>item!=null).ToArray();
                if(!selected.Contains(dragEntry))selected=new[]{dragEntry};
                dragEntry=null;
                try
                {
                    var paths=selected.Select(item=>manager.Files.EntryPath(Capsule,item)).ToArray();
                    if(paths.Any(path=>!File.Exists(path)&&!Directory.Exists(path)))throw new IOException(Lang.Text("有条目的文件已不存在，请先恢复原文件。","An item's file is missing. Restore it before dragging out.","項目のファイルが見つかりません。復元してからドラッグしてください。"));
                    var data=new DataObject();data.SetData(DataFormats.FileDrop,paths);data.SetData("M-Capsule.InternalDrag",Capsule.Id);
                    var allowed=selected.Any(item=>item.IsReference)?DragDropEffects.Copy:DragDropEffects.Copy|DragDropEffects.Move;
                    var result=DragDrop.DoDragDrop(list,data,allowed);
                    if(result==DragDropEffects.Copy||result==DragDropEffects.Move)manager.MoveEntriesOut(Capsule,selected);
                }
                catch(Exception ex){manager.Error(new IOException(Lang.Text("无法从胶囊拖出：","Could not drag out of capsule: ","カプセルからドラッグできません：")+ex.Message,ex));}
                e.Handled=true;
            };
            var menu=new ContextMenu();var open=new MenuItem{Header=Lang.Text("打开","Open","開く")};open.Click+=(s,e)=>LaunchSelected();menu.Items.Add(open);
            var remove=new MenuItem{Header=Lang.Text("删除","Remove","削除"),InputGestureText="Delete"};remove.Click+=(s,e)=>DeleteSelected();menu.Items.Add(remove);
            menu.Opened+=(s,e)=>{MenuOpen=true;open.IsEnabled=remove.IsEnabled=list.SelectedItems.Count>0;};
            menu.Closed+=(s,e)=>{MenuOpen=false;Dispatcher.BeginInvoke(new Action(()=>{if(IsVisible&&!IsActive&&manager.Dialogs==0&&manager.Popup==this)manager.ClosePopup();}),DispatcherPriority.Background);};list.ContextMenu=menu;
            PreviewKeyDown+=(s,e)=>
            {
                if(e.Key==Key.Escape){manager.ClosePopup();e.Handled=true;}
                else if(e.Key==Key.Delete){DeleteSelected();e.Handled=true;}
                else if(e.Key==Key.Enter){LaunchSelected();e.Handled=true;}
                else if(e.Key==Key.A&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){list.SelectAll();e.Handled=true;}
                else if(e.Key==Key.V&&(Keyboard.Modifiers&ModifierKeys.Control)!=0)
                {
                    if(Clipboard.ContainsFileDropList())manager.Add(Capsule,Clipboard.GetFileDropList().Cast<string>());e.Handled=true;
                }
            };
            DragOver+=(s,e)=>{CapsuleWindow.OnDragOver(s,e);if(String.Equals(e.Data.GetData("M-Capsule.InternalDrag") as string,Capsule.Id,StringComparison.OrdinalIgnoreCase))e.Effects=DragDropEffects.None;};
            Drop+=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop)&&!String.Equals(e.Data.GetData("M-Capsule.InternalDrag") as string,Capsule.Id,StringComparison.OrdinalIgnoreCase)){bool accepted=manager.Add(Capsule,(string[])e.Data.GetData(DataFormats.FileDrop));AcceptDrop();e.Effects=accepted?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;}};
            SourceInitialized+=(s,e)=>
            {
                Handle=new WindowInteropHelper(this).Handle;var source=HwndSource.FromHwnd(Handle);source.CompositionTarget.BackgroundColor=Colors.Transparent;
            };
            Loaded+=(s,e)=>ScheduleBackdrop();
            LocationChanged+=(s,e)=>ScheduleBackdrop();
            surface.SizeChanged+=(s,e)=>
            {
                if(surface.ActualWidth>0&&surface.ActualHeight>0)surface.Clip=new RectangleGeometry(new Rect(0,0,surface.ActualWidth,surface.ActualHeight),15,15);
                ScheduleBackdrop();
            };
            Deactivated+=(s,e)=>Dispatcher.BeginInvoke(new Action(()=>{if(!DragHover&&!IsActive&&!MenuOpen&&manager.Dialogs==0&&manager.Popup==this)manager.ClosePopup();}),DispatcherPriority.Background);
            Closed+=(s,e)=>{closed=true;renderVersion++;Interlocked.Increment(ref backdropVersion);backdropRefresh.Stop();backdrop.Source=null;list.Items.Clear();};
            Refresh();
        }
        void ScheduleBackdrop()
        {
            if(closed)return;
            Interlocked.Increment(ref backdropVersion);backdropRefresh.Stop();backdropRefresh.Start();
        }
        void RenderBackdrop()
        {
            if(closed||!IsVisible||backdrop.ActualWidth<=0||backdrop.ActualHeight<=0)return;
            var source=PresentationSource.FromVisual(this);if(source==null||source.CompositionTarget==null)return;
            var transform=source.CompositionTarget.TransformToDevice;
            var origin=backdrop.PointToScreen(new Point(0,0));
            var size=transform.Transform(new Vector(backdrop.ActualWidth,backdrop.ActualHeight));
            var panel=new Rect(origin.X,origin.Y,size.X,size.Y);int version=Thread.VolatileRead(ref backdropVersion);
            var worker=new Thread(()=>
            {
                try
                {
                    if(Thread.VolatileRead(ref backdropVersion)!=version)return;
                    var scene=WallpaperReader.Read();
                    if(Thread.VolatileRead(ref backdropVersion)!=version)return;
                    var image=GlassBackdrop.Render(scene,panel,transform.M11);
                    if(Thread.VolatileRead(ref backdropVersion)!=version)return;
                    Dispatcher.BeginInvoke(new Action(()=>
                    {
                        if(closed||backdropVersion!=version)return;
                        backdrop.Source=image;
                        // The tint is used only while loading or if a wallpaper cannot be read.
                        ((Border)surface.Children[1]).Visibility=image==null?Visibility.Visible:Visibility.Collapsed;
                        StartupLog.Write("popup backdrop="+(image==null?"transparent tint fallback":"wallpaper glass alpha="+GlassBackdrop.Opacity));
                    }));
                }
                catch(Exception ex){StartupLog.Write("popup backdrop failure "+ex.Message);}
            }){IsBackground=true,Name="Capsule wallpaper glass"};
            worker.SetApartmentState(ApartmentState.STA);worker.Start();
        }
        public void AcceptDrop(){if(closed)return;DragHover=false;Activate();Focus();}
        Style ItemStyle()
        {
            var s=new Style(typeof(ListBoxItem));
            s.Setters.Add(new Setter(WidthProperty,84.0));s.Setters.Add(new Setter(HeightProperty,88.0));s.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(3)));
            s.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.Transparent));s.Setters.Add(new Setter(Control.BorderBrushProperty,Brushes.Transparent));s.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));
            s.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Center));s.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty,VerticalAlignment.Center));
            var t=new ControlTemplate(typeof(ListBoxItem));var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(10));
            border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.BorderBrushProperty,new TemplateBindingExtension(Control.BorderBrushProperty));border.SetValue(Border.BorderThicknessProperty,new Thickness(1));border.SetValue(Border.PaddingProperty,new Thickness(3));
            var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(presenter);t.VisualTree=border;s.Setters.Add(new Setter(Control.TemplateProperty,t));
            var hover=new Trigger{Property=IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Control.BackgroundProperty,Ui.Brush(26,255,255,255)));s.Triggers.Add(hover);
            var selected=new Trigger{Property=ListBoxItem.IsSelectedProperty,Value=true};selected.Setters.Add(new Setter(Control.BackgroundProperty,Ui.Brush(65,83,157,245)));selected.Setters.Add(new Setter(Control.BorderBrushProperty,Ui.Brush(140,122,180,252)));s.Triggers.Add(selected);return s;
        }
        void LaunchSelected(){var cell=list.SelectedItem as ListBoxItem;if(cell!=null)manager.Launch(Capsule,(Shortcut)cell.Tag);}
        void DeleteSelected(){var selected=list.SelectedItems.Cast<ListBoxItem>().Select(x=>(Shortcut)x.Tag).ToArray();if(selected.Length>0)manager.DeleteEntries(Capsule,selected);}
        void ChangePage(int delta)
        {
            int next=pageIndex+delta;
            if(next<0||next>=pageCount)return;
            pageIndex=next;RenderItems();
        }
        public void Refresh(bool showLastPage=false)
        {
            var work=anchor.WorkArea();var dimensions=Logic.Layout(Capsule.Items.Count,work.Width,work.Height);Width=dimensions.Width;Height=dimensions.Height;
            pageSize=dimensions.PageSize;pageCount=Math.Max(1,(Capsule.Items.Count+pageSize-1)/pageSize);
            pageIndex=showLastPage?pageCount-1:Math.Min(pageIndex,pageCount-1);
            var wrap=new FrameworkElementFactory(typeof(WrapPanel));wrap.SetValue(FrameworkElement.WidthProperty,dimensions.Columns*84.0);wrap.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);list.ItemsPanel=new ItemsPanelTemplate(wrap);
            RenderItems();
            Position(work);
        }
        void RenderItems()
        {
            list.Items.Clear();int version=++renderVersion;
            foreach(var item in Capsule.Items.Skip(pageIndex*pageSize).Take(pageSize))
            {
                string path=manager.Files.EntryPath(Capsule,item);
                var iconImage=new Image{Source=Icons.Source(Capsule.ColorIndex),Width=32,Height=32,Margin=new Thickness(0,0,0,7)};
                var cached=manager.Icons.Get(Capsule.Id+item.Id,path,icon=>{if(!closed&&renderVersion==version)iconImage.Source=icon;});
                if(cached!=null)iconImage.Source=cached;
                var stack=new StackPanel{Width=74};stack.Children.Add(iconImage);
                var name=Ui.Text(item.Name,11,Ui.White);name.TextAlignment=TextAlignment.Center;name.TextWrapping=TextWrapping.Wrap;name.MaxHeight=31;stack.Children.Add(name);
                list.Items.Add(new ListBoxItem{Content=stack,Tag=item,ToolTip=item.Name,Cursor=Cursors.Arrow});
            }
            empty.Visibility=Capsule.Items.Count==0?Visibility.Visible:Visibility.Collapsed;
            list.Visibility=Capsule.Items.Count==0?Visibility.Collapsed:Visibility.Visible;
            count.Text=Capsule.Items.Count==0?
                Lang.Text("拖入文件保存副本 · 文件夹保存引用","Files are copied · Folders are linked","ファイルはコピー · フォルダーは参照"):
                pageCount>1?Lang.Text("第 ","Page ","ページ ")+(pageIndex+1)+"/"+pageCount+Lang.Text(" 页 · 共 "," · "," · 全 ")+Capsule.Items.Count+Lang.Text(" 个"," items"," 件"):
                Lang.Text("双击打开 · 拖出移走 · Delete 删除","Double-click to open · Drag out to remove","ダブルクリックで開く · ドラッグで移出");
            previousPage.Visibility=nextPage.Visibility=pageCount>1?Visibility.Visible:Visibility.Collapsed;
            previousPage.IsEnabled=pageIndex>0;nextPage.IsEnabled=pageIndex<pageCount-1;
        }
        void Position(Rect work)
        {
            double x=anchor.Left+anchor.Width+6;if(x+Width>work.Right-8)x=anchor.Left-Width-6;
            Left=Logic.Clamp(x,work.Left+8,work.Right-Width-8);Top=Logic.Clamp(anchor.Top,work.Top+8,work.Bottom-Height-8);
        }
    }
}
