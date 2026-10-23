using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PocketGrid
{
    internal static class Ui
    {
        public const string Product = "M-Capsule";
        public static SolidColorBrush Brush(byte a, byte r, byte g, byte b) { return new SolidColorBrush(Color.FromArgb(a,r,g,b)); }
        public static readonly Brush White = Brush(255,245,247,252), Muted = Brush(255,176,185,201);
        public static TextBlock Text(string text, double size, Brush color)
        {
            return new TextBlock { Text = text, FontSize = size, Foreground = color, FontFamily = new FontFamily("Microsoft YaHei UI"), TextTrimming = TextTrimming.CharacterEllipsis };
        }
        public static Button Button(string text, string tip)
        {
            var b = new Button { Content = text, ToolTip = tip, Cursor = Cursors.Hand, Background = Brush(25,255,255,255), Foreground = White, FontSize = 18, BorderThickness = new Thickness(0), FocusVisualStyle = null };
            var t = new ControlTemplate(typeof(Button));
            var frame = new FrameworkElementFactory(typeof(Border)); frame.SetValue(Border.CornerRadiusProperty,new CornerRadius(9));
            frame.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center); presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
            frame.AppendChild(presenter); t.VisualTree = frame; b.Template=t;
            b.MouseEnter += (s,e) => b.Background=Brush(50,255,255,255);
            b.MouseLeave += (s,e) => b.Background=Brush(25,255,255,255);
            return b;
        }
        public static string AskName(string title, string previous,Action replaceIcon=null,Action restoreIcon=null,Func<ImageSource> currentIcon=null,
            Action changeAppearance=null,Func<bool> hasCustomIcon=null,Func<int> opacityPercent=null)
        {
            bool hasIcon=replaceIcon!=null&&restoreIcon!=null&&currentIcon!=null;
            var w = new Window { Title = title, Width=hasIcon?490:340, Height=hasIcon?305:170, WindowStartupLocation=WindowStartupLocation.CenterScreen, ResizeMode=ResizeMode.NoResize, ShowInTaskbar=false, Topmost=true, Background=Brush(255,32,38,51), FontFamily=new FontFamily("Microsoft YaHei UI") };
            var stack=new StackPanel { Margin=new Thickness(20) };
            var input=new TextBox { Text=previous, MaxLength=40, FontSize=16, Padding=new Thickness(8), Margin=new Thickness(0,0,0,14) };
            var row=new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right };
            var cancel=new Button { Content=Lang.Text("取消","Cancel","キャンセル"), Width=70, Height=30, IsCancel=true, Margin=new Thickness(0,0,8,0) };
            var ok=new Button { Content=Lang.Text("确定","OK","決定"), Width=70, Height=30, IsDefault=true };
            ok.Click+=(s,e)=>{ if (!String.IsNullOrWhiteSpace(input.Text)) w.DialogResult=true; };
            if(hasIcon)
            {
                var hint=Text(CustomIcons.Hint,12,Muted);hint.TextWrapping=TextWrapping.Wrap;
                hint.Margin=new Thickness(0,0,0,8);stack.Children.Add(hint);
                var image=new Image{Source=currentIcon(),Width=48,Height=48,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,10,0),
                    Opacity=hasCustomIcon()!=true?opacityPercent()/100.0:1};
                var iconRow=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,14)};
                var replace=new Button{Content=Lang.Text("替换图标…","Set image…","画像を変更…"),Width=100,Height=32,Margin=new Thickness(0,8,8,0)};
                var restore=new Button{Content=Lang.Text("还原胶囊图","Restore image","画像を戻す"),Width=105,Height=32,Margin=new Thickness(0,8,8,0)};
                var color=new Button{Content=Lang.Text("换色/透明度…","Color/opacity…","色・透明度…"),Width=125,Height=32,Margin=new Thickness(0,8,0,0),IsEnabled=!hasCustomIcon()};
                Action refresh=()=>{image.Source=currentIcon();image.Opacity=hasCustomIcon()?1:opacityPercent()/100.0;color.IsEnabled=!hasCustomIcon();};
                replace.Click+=(s,e)=>{replaceIcon();refresh();};
                restore.Click+=(s,e)=>{restoreIcon();refresh();};
                color.Click+=(s,e)=>{changeAppearance();refresh();};
                iconRow.Children.Add(image);iconRow.Children.Add(replace);iconRow.Children.Add(restore);iconRow.Children.Add(color);stack.Children.Add(iconRow);
            }
            row.Children.Add(cancel);row.Children.Add(ok);stack.Children.Add(input);stack.Children.Add(row);w.Content=stack;
            w.Loaded+=(s,e)=>{input.Focus();input.SelectAll();};
            return w.ShowDialog()==true ? Logic.CleanName(input.Text) : null;
        }
    }
}
