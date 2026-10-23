using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Threading.Tasks;
using PocketGrid;

public static class UltimateOrganizerTests
{
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
    static bool Reject(Action action){try{action();return false;}catch{return true;}}
    public static void Main()
    {
        string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"organizer-sandbox-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string desktop=Path.Combine(root,"Desktop");Directory.CreateDirectory(desktop);
            string link=Path.Combine(desktop,"App.lnk"),photo=Path.Combine(desktop,"照片.png");
            File.WriteAllText(link,"shortcut");File.WriteAllText(photo,"photo");
            string sub=Path.Combine(desktop,"existing folder");Directory.CreateDirectory(sub);File.WriteAllText(Path.Combine(sub,"keep.txt"),"keep");
            File.WriteAllText(Path.Combine(desktop,"desktop.ini"),"system");
            string hidden=Path.Combine(desktop,"hidden.txt");File.WriteAllText(hidden,"hidden");File.SetAttributes(hidden,FileAttributes.Hidden);
            var plan=UltimateOrganizer.Prepare(desktop);
            Check(plan.Files.Length==2,"plan includes ordinary file and shortcut only");
            Check(!Directory.Exists(plan.Destination),"planning does not change desktop");
            Check(UltimateOrganizer.Execute(plan,false)==null&&File.Exists(link)&&!Directory.Exists(plan.Destination),"No leaves desktop unchanged");
            Check(Reject(()=>UltimateOrganizer.Execute(plan,true,i=>{if(i==1)throw new IOException("simulated failure");})),"simulated mid-move failure reported");
            Check(File.Exists(link)&&File.Exists(photo)&&!Directory.Exists(plan.Destination),"mid-move failure rolls back all moved files");
            string destination=UltimateOrganizer.Execute(plan,true);
            Check(Directory.Exists(destination)&&File.Exists(Path.Combine(destination,"App.lnk"))&&File.Exists(Path.Combine(destination,"照片.png")),"Yes moves both files into organizing folder");
            Check(!File.Exists(link)&&!File.Exists(photo),"desktop originals removed by move");
            Check(File.Exists(Path.Combine(sub,"keep.txt"))&&File.Exists(hidden)&&File.Exists(Path.Combine(desktop,"desktop.ini")),"folders and hidden system files preserved");
            Check(Reject(()=>UltimateOrganizer.Execute(plan,true)),"stale plan cannot move files twice");
            File.WriteAllText(link,"new shortcut");
            File.WriteAllText(photo,"new photo");
            var again=UltimateOrganizer.Prepare(desktop);
            Check(again.DestinationExists&&again.Files.Length==2,"second run reuses existing organizing folder");
            string reused=UltimateOrganizer.Execute(again,true);
            Check(reused==destination&&File.ReadAllText(Path.Combine(destination,"App.lnk"))=="shortcut"&&
                File.ReadAllText(Path.Combine(destination,"App (2).lnk"))=="new shortcut","second run keeps old file and uniquely names new shortcut");
            Check(File.ReadAllText(Path.Combine(destination,"照片.png"))=="photo"&&
                File.ReadAllText(Path.Combine(destination,"照片 (2).png"))=="new photo","second run keeps old photo and moves new one");
            File.WriteAllText(link,"third shortcut");File.WriteAllText(photo,"third photo");
            var retry=UltimateOrganizer.Prepare(desktop);
            Check(Reject(()=>UltimateOrganizer.Execute(retry,true,i=>{if(i==1)throw new IOException("second run simulated failure");})),
                "failure during repeated run is reported");
            Check(File.Exists(link)&&File.Exists(photo)&&Directory.Exists(destination)&&
                !File.Exists(Path.Combine(destination,"App (3).lnk")),"repeated-run failure rolls back and preserves existing folder");
            var left=ExplorerLayout.LeftHalf(new Rectangle(-1920,40,1920,1040));
            Check(left.Left==-1920&&left.Top==40&&left.Width==960&&left.Height==1040,"half-screen geometry respects negative monitor origin");
            RuntimeMode.Initialize(new[]{"--sandbox"});
            Check(ExplorerLayout.OpenOnLeft(destination,new Rectangle(0,0,1920,1080)).GetAwaiter().GetResult(),"sandbox window path never launches real Explorer");
            int polls=0;Rectangle placed=Rectangle.Empty;
            bool arranged=ExplorerLayout.Arrange(new Rectangle(-1920,40,1920,1040),
                ()=>++polls<3?IntPtr.Zero:new IntPtr(17),(hwnd,rect)=>{placed=rect;return hwnd==new IntPtr(17);},
                ()=>Task.FromResult(0),5).GetAwaiter().GetResult();
            Check(arranged&&polls==3&&placed==left,"simulated Explorer is positioned after window appears");
            bool missing=ExplorerLayout.Arrange(new Rectangle(0,0,1920,1080),()=>IntPtr.Zero,(hwnd,rect)=>true,
                ()=>Task.FromResult(0),3).GetAwaiter().GetResult();
            Check(!missing,"missing Explorer window reports layout failure");
            Console.WriteLine("PASS: "+checks+" isolated organizer checks; only temporary fixture directory touched.");
        }
        finally{File.SetAttributes(Path.Combine(root,"Desktop","hidden.txt"),FileAttributes.Normal);Directory.Delete(root,true);}
    }
}
