using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;
using Pen = System.Drawing.Pen;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace PocketGrid
{
    internal static class Icons
    {
        static readonly int[] Colors = { 0x282B32, 0x2777E8, 0x22966E, 0x845BD1, 0xDF7A29, 0xD85183, 0x269BAC, 0xB95043, 0x626EBB, 0x94713A };
        static readonly Dictionary<int, ImageSource> cache = new Dictionary<int, ImageSource>();
        public static Color Palette(int index)
        {
            if(index<0)return Color.FromArgb(237,65,81);
            if(index<Colors.Length)
            {
                int rgb=Colors[index];
                return Color.FromArgb(255,(rgb>>16)&255,(rgb>>8)&255,rgb&255);
            }
            // 120 further deterministic colors without assets or theme packages.
            int tone=(index-Colors.Length)%120;
            double hue=((tone%12)*30+(tone/12)*7)%360;
            double saturation=0.50+((tone/12)%5)*0.075;
            double lightness=0.38+((tone/60)*0.15)+((tone/12)%2)*0.045;
            return FromHsl(hue,saturation,lightness);
        }
        static Color FromHsl(double h,double s,double l)
        {
            double c=(1-Math.Abs(2*l-1))*s, x=c*(1-Math.Abs((h/60)%2-1)), m=l-c/2;
            double r=0,g=0,b=0;
            if(h<60){r=c;g=x;}else if(h<120){r=x;g=c;}else if(h<180){g=c;b=x;}
            else if(h<240){g=x;b=c;}else if(h<300){r=x;b=c;}else{r=c;b=x;}
            return Color.FromArgb((int)Math.Round((r+m)*255),(int)Math.Round((g+m)*255),(int)Math.Round((b+m)*255));
        }
        public static Bitmap Draw(int index, int pixels)
        {
            var bitmap = new Bitmap(pixels, pixels, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.ScaleTransform(pixels / 64f, pixels / 64f);
                g.TranslateTransform(32, 32); g.RotateTransform(-45);
                using (var pill = new GraphicsPath())
                {
                    pill.AddArc(-27, -14, 28, 28, 90, 180);
                    pill.AddArc(-1, -14, 28, 28, 270, 180); pill.CloseFigure();
                    var color = Palette(index);
                    var second = index < 0 ? Color.FromArgb(255, 207, 72) : Color.FromArgb(255, (color.R + 360) / 3, (color.G + 400) / 3, (color.B + 430) / 3);
                    using (var b = new SolidBrush(color)) g.FillPath(b, pill);
                    var state = g.Save(); g.SetClip(pill);
                    using (var b = new SolidBrush(second)) g.FillRectangle(b, 0, -15, 30, 30);
                    using (var b = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) g.FillEllipse(b, -20, -10, 33, 8);
                    g.Restore(state);
                    using (var p = new Pen(Color.FromArgb(135, 255, 255, 255), 1.2f)) g.DrawPath(p, pill);
                    using (var p = new Pen(Color.FromArgb(55, 0, 0, 0), 1f)) g.DrawLine(p, 0, -13, 0, 13);
                }
            }
            return bitmap;
        }
        public static ImageSource Source(int index)
        {
            ImageSource image;
            if (cache.TryGetValue(index, out image)) return image;
            using (var bitmap = Draw(index, 128))
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png); stream.Position = 0;
                var value = new BitmapImage(); value.BeginInit(); value.CacheOption = BitmapCacheOption.OnLoad; value.StreamSource = stream; value.EndInit(); value.Freeze();
                cache[index] = value; return value;
            }
        }
        public static void SaveIco(string file, int index)
        {
            using (var bitmap = Draw(index, 256))
            using (var png = new MemoryStream())
            {
                bitmap.Save(png, ImageFormat.Png); byte[] data = png.ToArray();
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                using (var writer = new BinaryWriter(File.Create(file)))
                {
                    writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)1);
                    writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0);
                    writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(data.Length); writer.Write(22); writer.Write(data);
                }
            }
        }
    }
}
