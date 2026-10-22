using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PocketGrid
{
    internal sealed class CapsuleListDialog : Window
    {
        readonly Manager manager;
        readonly StackPanel rows;
        public CapsuleListDialog(Manager manager)
        {
            this.manager=manager;
            Title="管理桌面胶囊";Width=660;Height=480;MinHeight=230;MinWidth=610;
            WindowStartupLocation=WindowStartupLocation.CenterScreen;
            Background=Brushes.White;ResizeMode=ResizeMode.CanResize;Topmost=true;
            var layout=new DockPanel{Margin=new Thickness(18)};Content=layout;
            var heading=new TextBlock{Text="桌面胶囊",FontSize=19,FontWeight=FontWeights.SemiBold};
            DockPanel.SetDock(heading,Dock.Top);layout.Children.Add(heading);
            var help=new TextBlock{Text="点击名称更名；可替换或还原每个胶囊的桌面图标。",Margin=new Thickness(0,8,0,4)};
            DockPanel.SetDock(help,Dock.Top);layout.Children.Add(help);
            var format=new TextBlock{Text=CustomIcons.Hint,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,Margin=new Thickness(0,0,0,12)};
            DockPanel.SetDock(format,Dock.Top);layout.Children.Add(format);
            var close=new Button{Content="关闭",Width=76,Height=30,HorizontalAlignment=HorizontalAlignment.Right,IsCancel=true};
            close.Click+=(s,e)=>Close();DockPanel.SetDock(close,Dock.Bottom);layout.Children.Add(close);
            rows=new StackPanel();layout.Children.Add(new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
            Refresh();
        }
        void Refresh()
        {
            rows.Children.Clear();
            foreach(var capsule in manager.Preferences.Capsules.ToArray())
            {
                var current=capsule;
                var row=new DockPanel{Margin=new Thickness(0,0,0,8)};
                var delete=new Button{Content="删除",Width=66,Height=30,Margin=new Thickness(8,0,0,0)};
                delete.Click+=(s,e)=>{manager.DeleteCapsule(current);Refresh();};
                DockPanel.SetDock(delete,Dock.Right);row.Children.Add(delete);
                var restore=new Button{Content="还原",Width=66,Height=30,Margin=new Thickness(8,0,0,0),IsEnabled=current.HasCustomIcon,ToolTip="还原原来的胶囊图"};
                restore.Click+=(s,e)=>{manager.RestoreIcon(current);Refresh();};
                DockPanel.SetDock(restore,Dock.Right);row.Children.Add(restore);
                var replace=new Button{Content="替换图标…",Width=88,Height=30,Margin=new Thickness(8,0,0,0),ToolTip=CustomIcons.Hint};
                replace.Click+=(s,e)=>{manager.ChangeIcon(current);Refresh();};
                DockPanel.SetDock(replace,Dock.Right);row.Children.Add(replace);
                var preview=new Image{Source=CustomIcons.Source(Settings.DataDirectory,current),Width=30,Height=30,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,7,0)};
                DockPanel.SetDock(preview,Dock.Left);row.Children.Add(preview);
                var name=new Button{Content=current.Name,Height=30,HorizontalContentAlignment=HorizontalAlignment.Left,
                    Padding=new Thickness(9,0,9,0),ToolTip="点击更名"};
                name.Click+=(s,e)=>{manager.Rename(current);Refresh();};
                row.Children.Add(name);rows.Children.Add(row);
            }
            if(rows.Children.Count==0)rows.Children.Add(new TextBlock{Text="桌面上暂无胶囊。",Margin=new Thickness(0,8,0,0)});
        }
    }
}
