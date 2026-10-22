using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;

namespace PocketGrid
{
    // Only the read-only prefix of IDesktopWallpaper is used. No wallpaper settings are changed.
    [ComImport,Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD")] class DesktopWallpaperObject { }
    [ComImport,Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDesktopWallpaperReader
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor,[MarshalAs(UnmanagedType.LPWStr)] string path);
        void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor,[MarshalAs(UnmanagedType.LPWStr)] out string path);
        void GetMonitorDevicePathAt(uint index,[MarshalAs(UnmanagedType.LPWStr)] out string monitor);
        void GetMonitorDevicePathCount(out uint count);
        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitor,out Native.Rect rect);
        void SetBackgroundColor(uint color);
        void GetBackgroundColor(out uint color);
        void SetPosition(int position);
        void GetPosition(out int position);
    }
    internal sealed class WallpaperImage
    {
        public BitmapSource Bitmap;
        public double Width,Height;
    }
    internal sealed class WallpaperLayer
    {
        public Rect Monitor;
        public WallpaperImage Image;
    }
    internal sealed class WallpaperScene
    {
        public readonly List<WallpaperLayer> Layers=new List<WallpaperLayer>();
        public int Position=4;
        public Color Background=Color.FromRgb(32,38,51);
        public Rect Bounds
        {
            get { Rect bounds=Rect.Empty;foreach(var layer in Layers)bounds.Union(layer.Monitor);return bounds; }
        }
    }
    internal static class WallpaperReader
    {
        static readonly Dictionary<string,WallpaperImage> cache=new Dictionary<string,WallpaperImage>(StringComparer.OrdinalIgnoreCase);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool SystemParametersInfo(uint action,uint size,StringBuilder value,uint flags);
        internal static WallpaperImage ReadImage(string path)
        {
            if(String.IsNullOrWhiteSpace(path)||!File.Exists(path))return null;
            string key=path+"|"+File.GetLastWriteTimeUtc(path).Ticks;
            lock(cache)
            {
                WallpaperImage existing;if(cache.TryGetValue(key,out existing))return existing;
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                {
                    var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand);
                    double width=decoder.Frames[0].PixelWidth,height=decoder.Frames[0].PixelHeight;
                    stream.Position=0;
                    var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;
                    if(Math.Max(width,height)>2048)bitmap.DecodePixelWidth=Math.Max(1,(int)(width*2048/Math.Max(width,height)));
                    bitmap.EndInit();bitmap.Freeze();
                    var result=new WallpaperImage{Bitmap=bitmap,Width=width,Height=height};
                    if(cache.Count>=4)cache.Clear();cache[key]=result;return result;
                }
            }
        }
        public static WallpaperScene Read()
        {
            if(RuntimeMode.Sandbox)return null;
            object instance=null;
            try
            {
                instance=new DesktopWallpaperObject();var reader=(IDesktopWallpaperReader)instance;
                var scene=new WallpaperScene();uint color,count;bool hasImagePath=false,hasImage=false;
                reader.GetBackgroundColor(out color);scene.Background=Color.FromRgb((byte)color,(byte)(color>>8),(byte)(color>>16));
                reader.GetPosition(out scene.Position);reader.GetMonitorDevicePathCount(out count);
                for(uint i=0;i<Math.Min(count,32u);i++)
                {
                    string monitor,path;Native.Rect rect;reader.GetMonitorDevicePathAt(i,out monitor);reader.GetMonitorRECT(monitor,out rect);reader.GetWallpaper(monitor,out path);
                    if(rect.Right<=rect.Left||rect.Bottom<=rect.Top)continue;
                    WallpaperImage image=null;
                    hasImagePath|=!String.IsNullOrWhiteSpace(path);
                    try{image=ReadImage(path);}catch(Exception ex){StartupLog.Write("wallpaper decode failed "+ex.Message);}
                    hasImage|=image!=null;
                    scene.Layers.Add(new WallpaperLayer{Monitor=new Rect(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top),Image=image});
                }
                if(scene.Layers.Count>0&&(!hasImagePath||hasImage))return scene;
            }
            catch(Exception ex){StartupLog.Write("wallpaper API fallback "+ex.Message);}
            finally{if(instance!=null)Marshal.FinalReleaseComObject(instance);}
            try
            {
                var path=new StringBuilder(32768);
                if(!SystemParametersInfo(0x0073,(uint)path.Capacity,path,0))return null;
                var image=ReadImage(path.ToString());if(image==null)return null;
                var scene=new WallpaperScene();
                using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                {
                    string style=key==null?null:key.GetValue("WallpaperStyle") as string;
                    scene.Position=key!=null&&(key.GetValue("TileWallpaper") as string)=="1"?1:style=="0"?0:style=="2"?2:style=="6"?3:style=="22"?5:4;
                }
                foreach(var screen in Forms.Screen.AllScreens){var r=screen.Bounds;scene.Layers.Add(new WallpaperLayer{Monitor=new Rect(r.X,r.Y,r.Width,r.Height),Image=image});}
                return scene;
            }
            catch(Exception ex){StartupLog.Write("wallpaper unavailable "+ex.Message);return null;}
        }
    }
    internal static class GlassBackdrop
    {
        public const byte Opacity=210;
        public static Rect ImageRectangle(Size image,Rect monitor,int position)
        {
            if(position==2)return monitor;
            double scale=position==0?1:position==3?Math.Min(monitor.Width/image.Width,monitor.Height/image.Height):Math.Max(monitor.Width/image.Width,monitor.Height/image.Height);
            double w=image.Width*scale,h=image.Height*scale;
            return new Rect(monitor.X+(monitor.Width-w)/2,monitor.Y+(monitor.Height-h)/2,w,h);
        }
        // A wallpaper sample is blurred offscreen, then given real per-pixel alpha.
        // No desktop screenshot, screen capture, or native acrylic success flag is involved.
        public static BitmapSource Render(WallpaperScene scene,Rect panel,double dpiScale)
        {
            if(scene==null||panel.IsEmpty||panel.Width<=0||panel.Height<=0)return null;
            double padding=Math.Ceiling(24*Math.Max(1,dpiScale));
            var expanded=panel;expanded.Inflate(padding,padding);
            double scale=Math.Min(1,800/Math.Max(expanded.Width,expanded.Height));
            int width=Math.Max(1,(int)Math.Ceiling(expanded.Width*scale)),height=Math.Max(1,(int)Math.Ceiling(expanded.Height*scale));
            var visual=new DrawingVisual();
            using(var dc=visual.RenderOpen())
            {
                dc.PushTransform(new MatrixTransform(scale,0,0,scale,-expanded.X*scale,-expanded.Y*scale));
                dc.DrawRectangle(new SolidColorBrush(scene.Background),null,expanded);
                foreach(var layer in scene.Layers)
                {
                    if(layer.Image==null||!layer.Monitor.IntersectsWith(expanded))continue;
                    dc.PushClip(new RectangleGeometry(layer.Monitor));
                    if(scene.Position==1)
                    {
                        var brush=new ImageBrush(layer.Image.Bitmap){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(scene.Bounds.Location,new Size(layer.Image.Width,layer.Image.Height)),Stretch=Stretch.Fill};
                        dc.DrawRectangle(brush,null,expanded);
                    }
                    else dc.DrawImage(layer.Image.Bitmap,ImageRectangle(new Size(layer.Image.Width,layer.Image.Height),scene.Position==5?scene.Bounds:layer.Monitor,scene.Position));
                    dc.Pop();
                }
                dc.Pop();
            }
            var target=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);target.Render(visual);
            var pixels=new byte[width*height*4];target.CopyPixels(pixels,width*4,0);
            Blur(pixels,width,height,Math.Max(1,(int)Math.Round(6*Math.Max(1,dpiScale)*scale)));
            int outputWidth=Math.Max(1,(int)Math.Ceiling(panel.Width*scale)),outputHeight=Math.Max(1,(int)Math.Ceiling(panel.Height*scale));
            int offset=(int)Math.Round(padding*scale);var output=new byte[outputWidth*outputHeight*4];
            for(int y=0;y<outputHeight;y++)for(int x=0;x<outputWidth;x++)
            {
                int source=(Math.Min(height-1,y+offset)*width+Math.Min(width-1,x+offset))*4,dest=(y*outputWidth+x)*4;
                output[dest]=(byte)Math.Round((pixels[source]*.78+37*.22)*Opacity/255.0);
                output[dest+1]=(byte)Math.Round((pixels[source+1]*.78+29*.22)*Opacity/255.0);
                output[dest+2]=(byte)Math.Round((pixels[source+2]*.78+25*.22)*Opacity/255.0);
                output[dest+3]=Opacity;
            }
            var result=BitmapSource.Create(outputWidth,outputHeight,96,96,PixelFormats.Pbgra32,null,output,outputWidth*4);result.Freeze();return result;
        }
        internal static void Blur(byte[] pixels,int width,int height,int radius)
        {
            var temporary=new byte[pixels.Length];int window=radius*2+1;
            for(int pass=0;pass<3;pass++)
            {
                for(int y=0;y<height;y++)for(int c=0;c<4;c++)
                {
                    int sum=0;for(int k=-radius;k<=radius;k++)sum+=pixels[(y*width+Math.Max(0,Math.Min(width-1,k)))*4+c];
                    for(int x=0;x<width;x++)
                    {
                        temporary[(y*width+x)*4+c]=(byte)(sum/window);
                        sum-=pixels[(y*width+Math.Max(0,x-radius))*4+c];sum+=pixels[(y*width+Math.Min(width-1,x+radius+1))*4+c];
                    }
                }
                for(int x=0;x<width;x++)for(int c=0;c<4;c++)
                {
                    int sum=0;for(int k=-radius;k<=radius;k++)sum+=temporary[(Math.Max(0,Math.Min(height-1,k))*width+x)*4+c];
                    for(int y=0;y<height;y++)
                    {
                        pixels[(y*width+x)*4+c]=(byte)(sum/window);
                        sum-=temporary[(Math.Max(0,y-radius)*width+x)*4+c];sum+=temporary[(Math.Min(height-1,y+radius+1)*width+x)*4+c];
                    }
                }
            }
        }
    }
}
