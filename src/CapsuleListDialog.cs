using System;
using System.Collections.Generic;
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
        readonly HashSet<string> selectedIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly TextBlock selectionCount;
        readonly Button batchDelete,batchImage,batchAppearance;
        readonly TextBlock heading,help,format;
        readonly Button selectAll,selectNone,close,languageButton;
        public CapsuleListDialog(Manager manager)
        {
            this.manager=manager;
            Width=Lang.Current=="zh"?820:1020;Height=520;MinHeight=270;MinWidth=790;
            WindowStartupLocation=WindowStartupLocation.CenterScreen;
            Background=Brushes.White;ResizeMode=ResizeMode.CanResize;Topmost=true;
            var layout=new DockPanel{Margin=new Thickness(18)};Content=layout;
            var titleRow=new Grid();
            heading=new TextBlock{FontSize=19,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center};titleRow.Children.Add(heading);
            languageButton=new Button{Content="⚙ 文/A",Width=88,Height=32,HorizontalAlignment=HorizontalAlignment.Right};
            languageButton.Click+=(s,e)=>{manager.SetLanguage(Lang.Next(Lang.Current));ApplyLanguage();Refresh();};
            titleRow.Children.Add(languageButton);DockPanel.SetDock(titleRow,Dock.Top);layout.Children.Add(titleRow);
            help=new TextBlock{Margin=new Thickness(0,8,0,4)};
            DockPanel.SetDock(help,Dock.Top);layout.Children.Add(help);
            format=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,Margin=new Thickness(0,0,0,12)};
            DockPanel.SetDock(format,Dock.Top);layout.Children.Add(format);
            var tools=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,12)};
            selectAll=new Button{Width=70,Height=30};selectAll.Click+=(s,e)=>{selectedIds.UnionWith(manager.Preferences.Capsules.Select(c=>c.Id));Refresh();};
            selectNone=new Button{Width=70,Height=30,Margin=new Thickness(6,0,0,0)};selectNone.Click+=(s,e)=>{selectedIds.Clear();Refresh();};
            selectionCount=new TextBlock{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,10,0)};
            batchImage=new Button{Width=128,Height=30,Margin=new Thickness(6,0,0,0)};
            batchImage.Click+=(s,e)=>{manager.ChangeIcons(Selected());Refresh();};
            batchAppearance=new Button{Width=142,Height=30,Margin=new Thickness(6,0,0,0)};
            batchAppearance.Click+=(s,e)=>{manager.ChangeAppearances(Selected());Refresh();};
            batchDelete=new Button{Width=98,Height=30,Margin=new Thickness(6,0,0,0)};
            batchDelete.Click+=(s,e)=>{manager.DeleteCapsules(Selected());Refresh();};
            foreach(var control in new UIElement[]{selectAll,selectNone,selectionCount,batchImage,batchAppearance,batchDelete})tools.Children.Add(control);
            DockPanel.SetDock(tools,Dock.Top);layout.Children.Add(tools);
            close=new Button{Width=76,Height=30,HorizontalAlignment=HorizontalAlignment.Right,IsCancel=true};
            close.Click+=(s,e)=>Close();DockPanel.SetDock(close,Dock.Bottom);layout.Children.Add(close);
            rows=new StackPanel();layout.Children.Add(new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
            ApplyLanguage();
            Refresh();
        }
        void ApplyLanguage()
        {
            Title=Lang.Text("胶囊管理","Capsule Manager","カプセル管理");
            heading.Text=Lang.Text("桌面胶囊","Desktop capsules","デスクトップのカプセル");
            help.Text=Lang.Text("勾选多个胶囊可批量删除、统一换图或换色；点击名称可单独更名。",
                "Select capsules to delete or apply one image/color to all. Click a name to rename it.",
                "複数選択で一括削除・画像変更・色変更。名前をクリックして個別に変更できます。");
            format.Text=CustomIcons.Hint;
            languageButton.ToolTip=Lang.Text("切换语言：中文 → English → 日本語","Language: English → 日本語 → 中文","言語切替：日本語 → 中文 → English");
            selectAll.Content=Lang.Text("全选","Select all","すべて選択");
            selectNone.Content=Lang.Text("清空","Clear","選択解除");
            batchImage.Content=Lang.Text("统一换图…","Set image for all…","画像を一括変更…");batchImage.ToolTip=CustomIcons.Hint;
            batchAppearance.Content=Lang.Text("统一换色…","Set color for all…","色を一括変更…");
            batchAppearance.ToolTip=Lang.Text("自定义图片胶囊会跳过","Capsules with custom images are skipped","カスタム画像のカプセルは対象外");
            batchDelete.Content=Lang.Text("批量删除","Delete selected","選択を削除");
            close.Content=Lang.Text("关闭","Close","閉じる");
            Width=Lang.Current=="zh"?820:1020;
        }
        Capsule[] Selected(){return manager.Preferences.Capsules.Where(c=>selectedIds.Contains(c.Id)).ToArray();}
        void UpdateSelectionControls()
        {
            int count=Selected().Length;
            selectionCount.Text=Lang.Text("已选 "+count+" 个",count+" selected",count+" 件選択中");
            batchDelete.IsEnabled=batchImage.IsEnabled=batchAppearance.IsEnabled=count>0;
        }
        void Refresh()
        {
            selectedIds.IntersectWith(manager.Preferences.Capsules.Select(c=>c.Id));
            rows.Children.Clear();
            foreach(var capsule in manager.Preferences.Capsules.ToArray())
            {
                var current=capsule;
                var row=new DockPanel{Margin=new Thickness(0,0,0,8)};
                var selection=new CheckBox{IsChecked=selectedIds.Contains(current.Id),Width=28,Height=30,VerticalContentAlignment=VerticalAlignment.Center,
                    ToolTip=Lang.Text("选中后可批量操作","Select for batch actions","一括操作の対象に選択")};
                selection.Checked+=(s,e)=>{selectedIds.Add(current.Id);UpdateSelectionControls();};
                selection.Unchecked+=(s,e)=>{selectedIds.Remove(current.Id);UpdateSelectionControls();};
                DockPanel.SetDock(selection,Dock.Left);row.Children.Add(selection);
                var delete=new Button{Content=Lang.Text("删除","Delete","削除"),Width=70,Height=30,Margin=new Thickness(8,0,0,0)};
                delete.Click+=(s,e)=>{manager.DeleteCapsule(current);Refresh();};
                DockPanel.SetDock(delete,Dock.Right);row.Children.Add(delete);
                var restore=new Button{Content=Lang.Text("还原","Restore","戻す"),Width=70,Height=30,Margin=new Thickness(8,0,0,0),IsEnabled=current.HasCustomIcon,
                    ToolTip=Lang.Text("还原原来的胶囊图","Restore default capsule image","既定のカプセル画像に戻す")};
                restore.Click+=(s,e)=>{manager.RestoreIcon(current);Refresh();};
                DockPanel.SetDock(restore,Dock.Right);row.Children.Add(restore);
                var replace=new Button{Content=Lang.Text("替换图标…","Set image…","画像を変更…"),Width=105,Height=30,Margin=new Thickness(8,0,0,0),ToolTip=CustomIcons.Hint};
                replace.Click+=(s,e)=>{manager.ChangeIcon(current);Refresh();};
                DockPanel.SetDock(replace,Dock.Right);row.Children.Add(replace);
                var appearance=new Button{Content=Lang.Text("换色/透明度…","Color/opacity…","色・透明度…"),Width=128,Height=30,Margin=new Thickness(8,0,0,0),
                    IsEnabled=!current.HasCustomIcon,ToolTip=current.HasCustomIcon?
                    Lang.Text("请先还原默认胶囊图","Restore the default image first","先に既定画像へ戻してください"):
                    Lang.Text("130 种颜色和 0–100% 透明度","130 colors and 0–100% opacity","130 色と透明度 0～100%")};
                appearance.Click+=(s,e)=>{manager.ChangeAppearance(current);Refresh();};
                DockPanel.SetDock(appearance,Dock.Right);row.Children.Add(appearance);
                var preview=new Image{Source=CustomIcons.Source(Settings.DataDirectory,current),Width=30,Height=30,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,7,0),
                    Opacity=current.HasCustomIcon?1:current.IconOpacityPercent/100.0};
                DockPanel.SetDock(preview,Dock.Left);row.Children.Add(preview);
                var name=new Button{Content=current.Name,Height=30,HorizontalContentAlignment=HorizontalAlignment.Left,
                    Padding=new Thickness(9,0,9,0),ToolTip=Lang.Text("点击更名","Click to rename","クリックして名前を変更")};
                name.Click+=(s,e)=>{manager.Rename(current);Refresh();};
                row.Children.Add(name);rows.Children.Add(row);
            }
            if(rows.Children.Count==0)rows.Children.Add(new TextBlock{Text=Lang.Text("桌面上暂无胶囊。","No desktop capsules yet.","カプセルはまだありません。"),Margin=new Thickness(0,8,0,0)});
            UpdateSelectionControls();
        }
    }
}
