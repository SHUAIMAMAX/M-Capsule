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
            Title="管理桌面胶囊";Width=390;Height=460;MinHeight=190;
            WindowStartupLocation=WindowStartupLocation.CenterScreen;
            Background=Brushes.White;ResizeMode=ResizeMode.CanResize;Topmost=true;
            var layout=new DockPanel{Margin=new Thickness(18)};Content=layout;
            var heading=new TextBlock{Text="桌面胶囊",FontSize=19,FontWeight=FontWeights.SemiBold};
            DockPanel.SetDock(heading,Dock.Top);layout.Children.Add(heading);
            var help=new TextBlock{Text="点击名称更名；点击右侧按钮删除。",Margin=new Thickness(0,8,0,12)};
            DockPanel.SetDock(help,Dock.Top);layout.Children.Add(help);
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
                var name=new Button{Content=current.Name,Height=30,HorizontalContentAlignment=HorizontalAlignment.Left,
                    Padding=new Thickness(9,0,9,0),ToolTip="点击更名"};
                name.Click+=(s,e)=>{manager.Rename(current);Refresh();};
                row.Children.Add(name);rows.Children.Add(row);
            }
            if(rows.Children.Count==0)rows.Children.Add(new TextBlock{Text="桌面上暂无胶囊。",Margin=new Thickness(0,8,0,0)});
        }
    }
}
