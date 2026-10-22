using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PocketGrid
{
    internal sealed class CapsuleAppearanceDialog : Window
    {
        public int SelectedColor { get; private set; }
        public int SelectedOpacity { get; private set; }

        public CapsuleAppearanceDialog(Capsule capsule)
        {
            if(capsule.HasCustomIcon)throw new InvalidOperationException("请先还原默认胶囊图，再更换颜色和透明度。");
            Title="胶囊换色 · "+capsule.Name;
            Width=490;Height=560;MinWidth=440;MinHeight=430;
            WindowStartupLocation=WindowStartupLocation.CenterScreen;
            Background=Brushes.White;Topmost=true;
            SelectedColor=capsule.ColorIndex%130;
            SelectedOpacity=Math.Max(0,Math.Min(100,capsule.IconOpacityPercent));
            var root=new DockPanel{Margin=new Thickness(16)};Content=root;
            var heading=new TextBlock{Text="选择胶囊颜色（130 色）",FontSize=17,FontWeight=FontWeights.SemiBold};
            DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
            var hint=new TextBlock{Text="仅调整默认胶囊图；自定义图片请先在管理页点“还原”。透明度只影响胶囊图，名称仍可见。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,12)};
            DockPanel.SetDock(hint,Dock.Top);root.Children.Add(hint);
            var previewRow=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,8)};
            var preview=new Image{Width=64,Height=64,Stretch=Stretch.Uniform,Source=Icons.Source(SelectedColor),Opacity=SelectedOpacity/100.0};
            previewRow.Children.Add(preview);
            var opacityPanel=new StackPanel{Margin=new Thickness(16,5,0,0)};
            var value=new TextBlock{Text="透明度："+SelectedOpacity+"%",Margin=new Thickness(0,0,0,5)};
            var slider=new Slider{Minimum=0,Maximum=100,Value=SelectedOpacity,IsSnapToTickEnabled=true,TickFrequency=1,Width=305};
            slider.ValueChanged+=(s,e)=>{SelectedOpacity=(int)Math.Round(slider.Value);value.Text="透明度："+SelectedOpacity+"%";preview.Opacity=SelectedOpacity/100.0;};
            opacityPanel.Children.Add(value);opacityPanel.Children.Add(slider);previewRow.Children.Add(opacityPanel);
            DockPanel.SetDock(previewRow,Dock.Top);root.Children.Add(previewRow);
            var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,10,0,0)};
            var cancel=new Button{Content="取消",Width=76,Height=30,IsCancel=true,Margin=new Thickness(0,0,8,0)};
            var confirm=new Button{Content="确定",Width=76,Height=30,IsDefault=true};
            confirm.Click+=(s,e)=>DialogResult=true;
            footer.Children.Add(cancel);footer.Children.Add(confirm);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var grid=new UniformGrid{Columns=10};
            for(int i=0;i<130;i++)
            {
                int index=i;
                var swatch=new Border{Background=new SolidColorBrush(Color.FromRgb(Icons.Palette(i).R,Icons.Palette(i).G,Icons.Palette(i).B)),
                    Width=32,Height=32,CornerRadius=new CornerRadius(6),BorderThickness=new Thickness(2),BorderBrush=i==SelectedColor?Brushes.Black:Brushes.Transparent,Margin=new Thickness(3)};
                var button=new Button{Content=swatch,Width=41,Height=41,Padding=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),ToolTip="颜色 "+(i+1)};
                button.Click+=(s,e)=>{SelectedColor=index;preview.Source=Icons.Source(index);foreach(var child in grid.Children){var tile=((Button)child).Content as Border;tile.BorderBrush=Brushes.Transparent;}swatch.BorderBrush=Brushes.Black;};
                grid.Children.Add(button);
            }
            root.Children.Add(new ScrollViewer{Content=grid,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        }
    }
}
