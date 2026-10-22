using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using PocketGrid;

public static class CustomIconTests
{
    [STAThread]
    public static void Main()
    {
        int checks=0;
        Action<bool,string> check=(ok,name)=>{checks++;if(!ok)throw new Exception(name);};
        Func<Action,bool> reject=action=>{try{action();return false;}catch{return true;}};
        string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"icon-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try
        {
            string data=Path.Combine(dir,"Data"),src=Path.Combine(dir,"icon.png");var capsule=new Capsule();
            using(var bmp=new Bitmap(256,256))
            {
                using(var g=Graphics.FromImage(bmp))g.Clear(Color.Red);
                bmp.Save(src,ImageFormat.Png);
            }
            byte[] original=File.ReadAllBytes(src);
            check(CustomIcons.Decode(src).PixelWidth==256,"decode valid PNG");
            CustomIcons.Save(data,capsule,src);
            string owned=CustomIcons.FilePath(data,capsule);
            check(File.Exists(owned)&&File.ReadAllBytes(src).SequenceEqual(original),"copy icon into Data without changing original");
            capsule.HasCustomIcon=true;
            check(CustomIcons.Source(data,capsule)!=null,"custom image loads");
            File.Delete(src);
            check(CustomIcons.Source(data,capsule)!=null,"original image may move after import");
            CustomIcons.Remove(data,capsule);capsule.HasCustomIcon=false;
            check(!File.Exists(owned)&&CustomIcons.Source(data,capsule)!=null,"restore removes owned image and shows default");
            check(reject(()=>CustomIcons.FilePath(data,new Capsule{Id="../wrong"})),"reject icon path traversal");
            string tooLarge=Path.Combine(dir,"large.png");
            using(var bmp=new Bitmap(1025,40))bmp.Save(tooLarge,ImageFormat.Png);
            check(reject(()=>CustomIcons.Decode(tooLarge)),"reject excessive image dimension");
            string invalid=Path.Combine(dir,"bad.png");File.WriteAllText(invalid,"not an image");
            check(reject(()=>CustomIcons.Decode(invalid)),"reject malformed image");
            string wrongFormat=Path.Combine(dir,"bad.gif");File.WriteAllBytes(wrongFormat,new byte[]{1,2,3});
            check(reject(()=>CustomIcons.Decode(wrongFormat)),"reject unsupported extension");
            string tooBig=Path.Combine(dir,"huge.png");using(var file=File.Create(tooBig))file.SetLength(CustomIcons.MaxBytes+1);
            check(reject(()=>CustomIcons.Decode(tooBig)),"reject over 2 MiB before decode");
            string ico=Path.Combine(dir,"test.ico");
            using(var appIcon=Icon.ExtractAssociatedIcon(typeof(CustomIconTests).Assembly.Location))
            using(var file=File.Create(ico))appIcon.Save(file);
            check(CustomIcons.Decode(ico).PixelWidth>=16,"decode actual ICO file");
            var colors=Enumerable.Range(0,130).Select(i=>Icons.Palette(i).ToArgb()).ToArray();
            check(colors.Distinct().Count()==130,"130 distinct default capsule colors");
            Console.WriteLine("PASS: "+checks+" custom icon and palette checks.");
        }
        finally{Directory.Delete(dir,true);}
    }
}
