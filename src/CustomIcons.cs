using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PocketGrid
{
    internal static class CustomIcons
    {
        public const long MaxBytes=2*1024*1024;
        public const int MaxDimension=1024;
        public static string Hint { get { return Lang.Text(
            "支持 PNG、JPG/JPEG、BMP、ICO；建议正方形 256×256 像素。最长边不超过 1024 像素，文件不超过 2 MiB。确认后保存到便携版 Data/Icons；原图片不受影响。",
            "PNG, JPG/JPEG, BMP or ICO. A square 256×256 image is recommended; max side 1024 px, max file 2 MiB. A copy is saved in Data/Icons; the original stays unchanged.",
            "PNG、JPG/JPEG、BMP、ICO に対応。256×256 の正方形を推奨。最大辺 1024 px、ファイル 2 MiB 以下。Data/Icons にコピーし、元画像は変更しません。"); } }
        static readonly string[] extensions={".png",".jpg",".jpeg",".bmp",".ico"};
        public static string FilePath(string dataDirectory,Capsule capsule)
        {
            if(capsule==null||!Logic.IsId(capsule.Id))throw new InvalidDataException("胶囊 ID 无效。");
            return Path.Combine(dataDirectory,"Icons",capsule.Id+".png");
        }
        public static BitmapSource Decode(string source)
        {
            if(!extensions.Contains(Path.GetExtension(source),StringComparer.OrdinalIgnoreCase))throw new InvalidDataException("图片格式仅支持 PNG、JPG/JPEG、BMP、ICO。");
            var info=new FileInfo(source);
            if(!info.Exists||info.Length==0||info.Length>MaxBytes)throw new InvalidDataException("图片文件需为 2 MiB 以内的有效文件。");
            using(var stream=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
                var frame=decoder.Frames[0];
                if(frame.PixelWidth<16||frame.PixelHeight<16||frame.PixelWidth>MaxDimension||frame.PixelHeight>MaxDimension)
                    throw new InvalidDataException("图片宽高须为 16–1024 像素；建议 256×256 正方形。");
                double scale=Math.Min(1,256.0/Math.Max(frame.PixelWidth,frame.PixelHeight));
                var bitmap=new TransformedBitmap(frame,new ScaleTransform(scale,scale));bitmap.Freeze();return bitmap;
            }
        }
        public static void Save(string dataDirectory,Capsule capsule,string source)
        {
            SaveImage(dataDirectory,capsule,Decode(source));
        }
        public static void SaveImage(string dataDirectory,Capsule capsule,BitmapSource image)
        {
            if(image==null)throw new ArgumentNullException("image");
            string destination=FilePath(dataDirectory,capsule),dir=Path.GetDirectoryName(destination);
            Directory.CreateDirectory(dir);
            string temp=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
                using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))encoder.Save(output);
                if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        public static ImageSource Source(string dataDirectory,Capsule capsule)
        {
            if(!capsule.HasCustomIcon)return Icons.Source(capsule.ColorIndex);
            string file=FilePath(dataDirectory,capsule);
            try
            {
                if(!File.Exists(file))return Icons.Source(capsule.ColorIndex);
                using(var stream=File.OpenRead(file))
                {
                    var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;
                    bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();return bitmap;
                }
            }
            catch{return Icons.Source(capsule.ColorIndex);}
        }
        public static void Remove(string dataDirectory,Capsule capsule)
        {
            string path=FilePath(dataDirectory,capsule);
            if(File.Exists(path))File.Delete(path);
        }
    }
}
