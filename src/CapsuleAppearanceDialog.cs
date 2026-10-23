using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PocketGrid
{
    internal enum AppearanceEditMode { Combined, ColorOnly, OpacityOnly }
    internal sealed class CapsuleAppearanceDialog : Window
    {
        public int SelectedColor { get; private set; }
        public int SelectedOpacity { get; private set; }

        public CapsuleAppearanceDialog(Capsule capsule,AppearanceEditMode mode=AppearanceEditMode.Combined,ImageSource customImage=null)
        {
            if(mode!=AppearanceEditMode.OpacityOnly&&capsule.HasCustomIcon)throw new InvalidOperationException("请先还原默认胶囊图，再更换颜色。");
            Title=(mode==AppearanceEditMode.OpacityOnly?Lang.Text("胶囊透明度 · ","Capsule transparency · ","カプセルの透明度 · "):
                Lang.Text("胶囊换色 · ","Capsule color · ","カプセルの色 · "))+capsule.Name;
            Width=490;Height=mode==AppearanceEditMode.OpacityOnly?235:560;
            MinWidth=440;MinHeight=mode==AppearanceEditMode.OpacityOnly?220:430;
            WindowStartupLocation=WindowStartupLocation.CenterScreen;
            Background=Brushes.White;Topmost=true;
            SelectedColor=capsule.ColorIndex%130;
            SelectedOpacity=Math.Max(0,Math.Min(100,capsule.IconOpacityPercent));
            var root=new DockPanel{Margin=new Thickness(16)};Content=root;
            var heading=new TextBlock{Text=mode==AppearanceEditMode.OpacityOnly?
                Lang.Text("调整胶囊透明度","Adjust capsule transparency","カプセルの透明度を調整"):
                Lang.Text("选择胶囊颜色（130 色）","Choose a capsule color (130 colors)","カプセルの色を選択（130 色）"),FontSize=17,FontWeight=FontWeights.SemiBold};
            DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
            var hint=new TextBlock{Text=mode==AppearanceEditMode.OpacityOnly?
                Lang.Text("0% 不透明，100% 完全透明；只调整图案，名称保持可见。","0% is opaque; 100% is invisible. Only the image changes; the name remains visible.","0% は不透明、100% は完全透明。名前は表示されたままです。"):
                Lang.Text("仅调整默认胶囊图；自定义图片请先在胶囊管理中还原。","Only the default capsule image can change color. Restore a custom image in Capsule Manager first.","色を変更できるのは既定画像のみです。カスタム画像は管理画面で戻してください。"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,12)};
            DockPanel.SetDock(hint,Dock.Top);root.Children.Add(hint);
            var previewRow=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,8)};
            var preview=new Image{Width=64,Height=64,Stretch=Stretch.Uniform,
                Source=mode==AppearanceEditMode.OpacityOnly&&customImage!=null?customImage:Icons.Source(SelectedColor),Opacity=SelectedOpacity/100.0};
            previewRow.Children.Add(preview);
            if(mode!=AppearanceEditMode.ColorOnly)
            {
                var opacityPanel=new StackPanel{Margin=new Thickness(16,5,0,0)};
                bool transparency=mode==AppearanceEditMode.OpacityOnly;
                Func<int,string> display=amount=>transparency?
                    Lang.Text("透明度：","Transparency: ","透明度：")+(100-amount)+"%":
                    Lang.Text("不透明度：","Opacity: ","不透明度：")+amount+"%";
                var value=new TextBlock{Text=display(SelectedOpacity),Margin=new Thickness(0,0,0,5)};
                var slider=new Slider{Minimum=0,Maximum=100,Value=transparency?100-SelectedOpacity:SelectedOpacity,
                    IsSnapToTickEnabled=true,TickFrequency=1,Width=305};
                slider.ValueChanged+=(s,e)=>{SelectedOpacity=transparency?100-(int)Math.Round(slider.Value):(int)Math.Round(slider.Value);
                    value.Text=display(SelectedOpacity);preview.Opacity=SelectedOpacity/100.0;};
                opacityPanel.Children.Add(value);opacityPanel.Children.Add(slider);previewRow.Children.Add(opacityPanel);
            }
            DockPanel.SetDock(previewRow,Dock.Top);root.Children.Add(previewRow);
            var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,10,0,0)};
            var cancel=new Button{Content=Lang.Text("取消","Cancel","キャンセル"),Width=76,Height=30,IsCancel=true,Margin=new Thickness(0,0,8,0)};
            var confirm=new Button{Content=Lang.Text("确定","Apply","適用"),Width=76,Height=30,IsDefault=true};
            confirm.Click+=(s,e)=>DialogResult=true;
            footer.Children.Add(cancel);footer.Children.Add(confirm);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            if(mode!=AppearanceEditMode.OpacityOnly)
            {
                var grid=new UniformGrid{Columns=10};
                for(int i=0;i<130;i++)
                {
                    int index=i;
                    var swatch=new Border{Background=new SolidColorBrush(Color.FromRgb(Icons.Palette(i).R,Icons.Palette(i).G,Icons.Palette(i).B)),
                        Width=32,Height=32,CornerRadius=new CornerRadius(6),BorderThickness=new Thickness(2),BorderBrush=i==SelectedColor?Brushes.Black:Brushes.Transparent,Margin=new Thickness(3)};
                    var button=new Button{Content=swatch,Width=41,Height=41,Padding=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),ToolTip=Lang.Text("颜色 ","Color ","色 ")+(i+1)};
                    button.Click+=(s,e)=>{SelectedColor=index;preview.Source=Icons.Source(index);foreach(var child in grid.Children){var tile=((Button)child).Content as Border;tile.BorderBrush=Brushes.Transparent;}swatch.BorderBrush=Brushes.Black;};
                    grid.Children.Add(button);
                }
                root.Children.Add(new ScrollViewer{Content=grid,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
            }
        }
    }
}
