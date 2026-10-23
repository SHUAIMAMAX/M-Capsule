using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using PocketGrid;

public static class CustomIconTests
{
    static T Find<T>(DependencyObject parent) where T:DependencyObject
    {
        if(parent is T)return (T)parent;
        foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            var found=Find<T>(child);if(found!=null)return found;
        }
        return null;
    }
    static System.Collections.Generic.IEnumerable<T> All<T>(DependencyObject parent) where T:DependencyObject
    {
        if(parent is T)yield return (T)parent;
        foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            foreach(var found in All<T>(child))yield return found;
    }
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
            check(Enumerable.Range(0,130).All(i=>Icons.Palette(i).A==255),"every default capsule color is fully opaque");
            check(new Capsule().IconOpacityPercent==100,"new capsule defaults to fully opaque icon");
            check(Icons.Source(1) is System.Windows.Media.DrawingImage,"default desktop capsule uses scalable vector geometry");
            var sample=new Capsule{ColorIndex=4,IconOpacityPercent=75};
            var colorDialog=new CapsuleAppearanceDialog(sample,AppearanceEditMode.ColorOnly);
            check(Find<UniformGrid>(colorDialog)!=null&&Find<Slider>(colorDialog)==null,"quick color dialog shows palette without opacity control");
            var opacityDialog=new CapsuleAppearanceDialog(sample,AppearanceEditMode.OpacityOnly,Icons.Source(4));
            var opacitySlider=Find<Slider>(opacityDialog);
            check(opacitySlider!=null&&Find<UniformGrid>(opacityDialog)==null,"quick transparency dialog shows slider without color palette");
            opacitySlider.Value=40;
            check(opacityDialog.SelectedOpacity==60&&sample.IconOpacityPercent==75,"40 percent transparency maps to 60 percent image opacity without editing the model before confirmation");
            var custom=new Capsule{HasCustomIcon=true,IconOpacityPercent=100};
            check(new CapsuleAppearanceDialog(custom,AppearanceEditMode.OpacityOnly,Icons.Source(2))!=null,"custom image allows independent transparency adjustment");
            check(reject(()=>new CapsuleAppearanceDialog(custom,AppearanceEditMode.ColorOnly)),"custom image still blocks capsule color replacement");
            var desktopCapsule=new CapsuleWindow(null,sample);
            var menu=desktopCapsule.ContextMenu;
            check(menu.Items.Count==7&&menu.Items[5] is Separator,"desktop right-click keeps four direct actions and a separate manager entry");
            check(new[]{1,2,3,4}.All(i=>menu.Items[i] is MenuItem),"rename, color, transparency and delete are separate clickable commands");
            var desktopImage=(System.Windows.Controls.Image)((Border)((Grid)desktopCapsule.Content).Children[0]).Child;
            check(Math.Abs(desktopImage.Opacity-.75)<.001,"desktop capsule image reflects stored opacity");
            sample.HasCustomIcon=true;desktopCapsule.UpdateIcon();
            check(Math.Abs(desktopImage.Opacity-.75)<.001,"custom desktop image follows the same opacity setting");
            using(var pill=Icons.Draw(1,64))
                check(pill.GetPixel(23,40).A>=250&&pill.GetPixel(40,23).A>=250,"both pill halves render opaque at the default setting");
            var visual=new System.Windows.Media.DrawingVisual();
            using(var context=visual.RenderOpen())context.DrawImage(Icons.Source(1),new Rect(0,0,64,64));
            var rendered=new System.Windows.Media.Imaging.RenderTargetBitmap(64,64,96,96,System.Windows.Media.PixelFormats.Pbgra32);
            rendered.Render(visual);
            var pixels=new byte[64*64*4];rendered.CopyPixels(pixels,64*4,0);
            check(pixels[(40*64+23)*4+3]==255&&pixels[(23*64+40)*4+3]==255,
                "vector desktop capsule interior is fully opaque on both halves");
            var manager=(Manager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Manager));
            typeof(Manager).GetField("Preferences").SetValue(manager,new Preferences());
            var capsuleManager=new CapsuleListDialog(manager);
            check(All<Button>(capsuleManager).Any(b=>"+".Equals(b.Content)&&b.Background is System.Windows.Media.SolidColorBrush),
                "manager has a green plus creation button without showing a window");
            check(All<Hyperlink>(capsuleManager).Any(h=>h.Inlines.OfType<Run>().Any(r=>r.Text.Contains("开机自启"))),
                "manager has an underlined sign-in startup link");
            var tray=(System.Windows.Forms.ContextMenuStrip)typeof(Manager).GetMethod("BuildTray",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(manager,null);
            try
            {
                check(tray.Items.Count==9&&tray.Items[3] is System.Windows.Forms.ToolStripSeparator&&
                    tray.Items[6] is System.Windows.Forms.ToolStripSeparator,
                    "tray menu contains exactly three requested groups");
                check(tray.Items[0].Text.Contains("胶囊管理")&&tray.Items[7].Text=="帮助"&&tray.Items[8].Text=="退出",
                    "tray menu begins with manager and ends with help and exit");
            }
            finally{tray.Dispose();}
            Console.WriteLine("PASS: "+checks+" custom icon and palette checks.");
        }
        finally{Directory.Delete(dir,true);}
    }
}
