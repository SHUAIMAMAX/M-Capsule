using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PocketGrid;

public static class GlassBackdropTests
{
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception("FAIL "+label);checks++;Console.WriteLine("PASS "+label);}
    static WallpaperImage Split(int width,int height,Color left,Color right)
    {
        var pixels=new byte[width*height*4];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {var c=x<width/2?left:right;int p=(y*width+x)*4;pixels[p]=c.B;pixels[p+1]=c.G;pixels[p+2]=c.R;pixels[p+3]=255;}
        var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Pbgra32,null,pixels,width*4);bitmap.Freeze();
        return new WallpaperImage{Bitmap=bitmap,Width=width,Height=height};
    }
    static byte[] Pixel(BitmapSource bitmap,int x,int y)
    {var p=new byte[4];bitmap.CopyPixels(new Int32Rect(x,y,1,1),p,4,0);return p;}
    static void Save(BitmapSource source,string name)
    {var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name)))encoder.Save(file);}
    [STAThread] public static int Main()
    {
        RuntimeMode.Initialize(new[]{"--sandbox"});
        try
        {
            var monitor=new Rect(-400,100,400,400);var wide=new Size(800,400);
            Check(GlassBackdrop.ImageRectangle(wide,monitor,2)==monitor,"stretch preserves monitor rectangle including negative origin");
            Check(GlassBackdrop.ImageRectangle(wide,monitor,0)==new Rect(-600,100,800,400),"center keeps original pixel size");
            Check(GlassBackdrop.ImageRectangle(wide,monitor,3)==new Rect(-400,200,400,200),"fit preserves aspect ratio with letterbox");
            Check(GlassBackdrop.ImageRectangle(wide,monitor,4)==new Rect(-600,100,800,400),"fill crops centered wallpaper");
            var constant=new byte[64*32*4];for(int i=0;i<constant.Length;i+=4){constant[i]=40;constant[i+1]=80;constant[i+2]=120;constant[i+3]=255;}
            GlassBackdrop.Blur(constant,64,32,6);
            bool stable=true;for(int i=0;i<constant.Length;i+=4)stable&=constant[i]==40&&constant[i+1]==80&&constant[i+2]==120&&constant[i+3]==255;
            Check(stable,"blur preserves constant color and alpha at all edges");
            var edge=Split(256,128,Colors.Red,Colors.Blue);var scene=new WallpaperScene{Position=2};scene.Layers.Add(new WallpaperLayer{Monitor=new Rect(0,0,256,128),Image=edge});
            var full=GlassBackdrop.Render(scene,new Rect(0,0,256,128),1);
            Save(edge.Bitmap,"wallpaper-read-fixture.png");
            var loaded=WallpaperReader.ReadImage(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wallpaper-read-fixture.png"));
            Check(loaded!=null&&loaded.Bitmap.IsFrozen&&loaded.Width==256&&loaded.Height==128,"wallpaper file decoder loads a frozen image and preserves physical dimensions");
            Check(WallpaperReader.ReadImage(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"missing-wallpaper.png"))==null,"missing wallpaper file is handled without an opaque substitute");
            var largeFixture=Split(4096,256,Colors.Red,Colors.Blue);Save(largeFixture.Bitmap,"wallpaper-large-fixture.png");
            var decoded=WallpaperReader.ReadImage(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wallpaper-large-fixture.png"));
            Check(decoded.Width==4096&&decoded.Bitmap.PixelWidth==2048,"large wallpaper is decoded at bounded resolution without losing placement dimensions");
            Check(full.IsFrozen&&full.Format==PixelFormats.Pbgra32,"background is frozen and uses premultiplied per-pixel alpha");
            var bytes=new byte[full.PixelWidth*full.PixelHeight*4];full.CopyPixels(bytes,full.PixelWidth*4,0);
            bool translucent=true,valid=true;for(int i=0;i<bytes.Length;i+=4){translucent&=bytes[i+3]==210;valid&=bytes[i]<=bytes[i+3]&&bytes[i+1]<=bytes[i+3]&&bytes[i+2]<=bytes[i+3];}
            Check(translucent&&valid,"every glass pixel retains transparency with valid premultiplied RGB");
            var left=Pixel(full,30,64);var right=Pixel(full,225,64);var blend=Pixel(full,126,64);
            Check(left[2]>left[0]+100&&right[0]>right[2]+100,"wallpaper colors remain visible instead of a solid black fill");
            Check(blend[0]>30&&blend[2]>30,"a hard wallpaper edge becomes a blurred color transition");
            var cropped=GlassBackdrop.Render(scene,new Rect(112,32,32,64),1);var matching=Pixel(cropped,14,32);
            Check(Math.Abs(matching[0]-blend[0])<3&&Math.Abs(matching[2]-blend[2])<3,"partial popup samples the correct desktop position with blur padding");
            var high=GlassBackdrop.Render(scene,new Rect(0,0,3840,2160),2);
            Check(high.PixelWidth<=800&&high.PixelHeight<=800,"high DPI rendering has bounded background texture size");
            var multiple=new WallpaperScene{Position=2};
            multiple.Layers.Add(new WallpaperLayer{Monitor=new Rect(-300,0,300,200),Image=Split(100,100,Colors.Red,Colors.Red)});
            multiple.Layers.Add(new WallpaperLayer{Monitor=new Rect(0,0,300,200),Image=Split(100,100,Colors.Blue,Colors.Blue)});
            var crossing=GlassBackdrop.Render(multiple,new Rect(-60,30,120,80),1);left=Pixel(crossing,10,30);right=Pixel(crossing,110,30);
            Check(left[2]>left[0]+100&&right[0]>right[2]+100,"per-monitor wallpapers align across a negative-coordinate display boundary");
            scene.Position=1;scene.Layers[0].Monitor=new Rect(0,0,1024,256);
            var tiled=GlassBackdrop.Render(scene,new Rect(128,40,512,100),1);left=Pixel(tiled,64,50);right=Pixel(tiled,192,50);
            Check(left[0]>left[2]+100&&right[2]>right[0]+100,"tiled wallpaper phase stays anchored to the desktop rather than the popup");
            scene.Position=5;scene.Layers[0].Monitor=new Rect(-256,0,512,128);
            var span=GlassBackdrop.Render(scene,new Rect(-220,20,440,80),1);left=Pixel(span,30,30);right=Pixel(span,410,30);
            Check(left[2]>left[0]+100&&right[0]>right[2]+100,"span wallpaper uses virtual desktop coordinates");
            Check(GlassBackdrop.Render(null,new Rect(0,0,100,100),1)==null,"unavailable wallpaper requests a translucent fallback");
            var solid=new WallpaperScene{Background=Colors.DarkViolet};solid.Layers.Add(new WallpaperLayer{Monitor=new Rect(0,0,300,200)});
            Check(Pixel(GlassBackdrop.Render(solid,new Rect(20,20,100,100),1),50,50)[3]==210,"solid-color desktops still retain real transparency");

            // Export only synthetic test content, never a screenshot of the user's desktop.
            var visual=new DrawingVisual();
            var gradient=new LinearGradientBrush(Color.FromRgb(100,0,156),Color.FromRgb(0,85,172),new Point(0,0),new Point(1,1));
            var wallpaperVisual=new DrawingVisual();using(var dc=wallpaperVisual.RenderOpen())
            {
                dc.DrawRectangle(gradient,null,new Rect(0,0,640,420));
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(235,12,128)),null,new Rect(0,280,640,42));
                for(int x=0;x<640;x+=40)dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(70,255,255,255)),2),new Point(x,0),new Point(x,420));
            }
            var wallpaper=new RenderTargetBitmap(640,420,96,96,PixelFormats.Pbgra32);wallpaper.Render(wallpaperVisual);wallpaper.Freeze();
            var fixture=new WallpaperScene{Position=2};fixture.Layers.Add(new WallpaperLayer{Monitor=new Rect(0,0,640,420),Image=new WallpaperImage{Bitmap=wallpaper,Width=640,Height=420}});
            var glass=GlassBackdrop.Render(fixture,new Rect(100,65,440,290),1);
            using(var dc=visual.RenderOpen())
            {
                dc.DrawImage(wallpaper,new Rect(0,0,640,420));dc.PushClip(new RectangleGeometry(new Rect(100,65,440,290),16,16));dc.DrawImage(glass,new Rect(100,65,440,290));dc.Pop();
                dc.DrawRoundedRectangle(null,new Pen(new SolidColorBrush(Color.FromArgb(90,255,255,255)),1),new Rect(100,65,440,290),16,16);
                dc.DrawText(new FormattedText("离屏渲染测试 · 非实机截图",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),18,Brushes.White,1.0),new Point(126,86));
                for(int i=0;i<4;i++){dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(50,(byte)(130+i*20),240)),null,new Point(160+i*100,176),19,19);dc.DrawText(new FormattedText("快捷方式",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),12,Brushes.White,1.0),new Point(136+i*100,211));}
            }
            var preview=new RenderTargetBitmap(640,420,96,96,PixelFormats.Pbgra32);preview.Render(visual);Save(preview,"glass-offscreen-test.png");Save(full,"glass-alpha-test.png");
            Console.WriteLine("TOTAL PASS "+checks+". Synthetic offscreen rendering only; no windows opened or desktop captured.");return 0;
        }
        catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
}
