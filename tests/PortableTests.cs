using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Threading;
using PocketGrid;

public static class PortableTests
{
    static int count;
    static readonly List<string> results=new List<string>();
    static void Check(bool ok,string name) { if(!ok) throw new Exception("FAIL: "+name); count++; results.Add("PASS "+name); Console.WriteLine("PASS "+name); }
    static bool Reject(Action a) { try { a(); return false; } catch { return true; } }
    sealed class MemoryStartup : IStartupStore
    {
        public string Value; public int Deletes;
        public string Read(){return Value;}
        public void Write(string s){Value=s;}
        public void Delete(){Value=null;Deletes++;}
    }
    [STAThread] public static int Main()
    {
        Console.OutputEncoding=Encoding.UTF8;
        RuntimeMode.Initialize(new[]{"--sandbox"});
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        Manager manager=null;
        int exitCode=0;
        try
        {
            Check(RuntimeMode.Sandbox&&Settings.DataDirectory.EndsWith("SandboxData"),"test data uses isolated directory");
            Check(RuntimeMode.Identity.Contains("Sandbox"),"sandbox IPC identity differs from production");
            var empty=Settings.Load();
            Check(empty.Capsules.Count==1&&Settings.LoadNotice==null,"fresh portable configuration");
            Settings.Save(empty);
            var reloaded=Settings.Load();
            Check(reloaded.Capsules[0].Id==empty.Capsules[0].Id&&Settings.LoadNotice==null,"healthy disk reload has no false recovery warning");
            empty.Capsules[0].Name="中文 & Capsule";Settings.Save(empty);
            File.WriteAllText(Settings.Snapshot,"broken");
            var recovered=Settings.Load();
            Check(recovered.Capsules[0].Id==empty.Capsules[0].Id&&Settings.LoadNotice!=null,"corrupt primary recovers previous valid snapshot");
            Settings.Save(recovered);
            Check(Directory.GetFiles(Path.GetDirectoryName(Settings.Snapshot),"*.damaged-*").Length==1,"corrupt file retained for inspection");
            string disk=File.ReadAllText(Settings.Snapshot);
            var newer=Logic.Deserialize(disk);newer.Revision+=10;newer.Capsules[0].Name="中断写入恢复";
            File.WriteAllText(Settings.Snapshot+".pending",Logic.Serialize(newer));
            Check(Settings.Load().Capsules[0].Name=="中断写入恢复","complete pending write wins by revision");
            Settings.Save(newer);
            Check(Settings.LoadNotice!=null,"pending recovery reported");
            Settings.Load();
            Check(Settings.LoadNotice==null,"notice reset after healthy reload");
            long revision=newer.Revision;
            using(var locked=new FileStream(Settings.Snapshot+".pending",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
                Check(Reject(()=>Settings.Save(newer))&&newer.Revision==revision,"failed save rolls back revision");
            File.Delete(Settings.Snapshot+".pending");

            var memory=new MemoryStartup();
            string quoted=Path.Combine(Settings.InstallDirectory,"中文 & spaces","M-Capsule.exe");
            var startup=new StartupRegistration(memory,quoted);
            Check(!startup.Enabled,"autostart is opt-in");
            startup.Set(true);
            Check(startup.Enabled&&memory.Value=="\""+quoted+"\" --background","startup quotes Chinese, spaces and ampersand paths");
            startup.Set(true);Check(startup.Enabled,"startup enable is idempotent");
            startup.Set(false);Check(memory.Value==null&&memory.Deletes==1,"startup disable removes owned value");
            startup.Set(false);Check(memory.Deletes==1,"startup disable is idempotent");
            memory.Value="another portable copy";
            startup.Set(false);Check(memory.Value=="another portable copy","disabling does not delete another copy's startup");
            startup.Set(true);Check(startup.Enabled,"explicit enable updates moved directory");
            string before=memory.Value;
            Check(Reject(()=>new StartupRegistration(memory,Path.Combine(Settings.InstallDirectory,new string('x',270),"app.exe")).Set(true))&&memory.Value==before,"long startup path rejected before mutation");
            Settings.SetStartup(true);
            Check(Settings.StartupEnabled()&&File.Exists(Path.Combine(Settings.DataDirectory,"SimulatedSystem","startup.txt")),"sandbox startup uses file backend");
            Settings.SetStartup(false);Check(!Settings.StartupEnabled(),"sandbox startup disable");
            Check(Reject(()=>new TaskbarController(s=>{})),"sandbox refuses native taskbar controller construction");

            manager=new Manager();
            Check(manager.Windows.Count==manager.Preferences.Capsules.Count,"real WPF capsule windows created");
            var capsule=manager.CreateCapsule("测试胶囊");
            Check(manager.Windows.ContainsKey(capsule.Id),"create capsule updates model and real window");
            manager.RenameCapsule(capsule,"中文 & 重命名");
            Check(capsule.Name=="中文 & 重命名"&&Settings.Load().Capsules.Any(c=>c.Name==capsule.Name),"rename persists Unicode");
            string fixtures=Path.Combine(Settings.DataDirectory,"Fixtures");Directory.CreateDirectory(fixtures);
            string link=Path.Combine(fixtures,"测试快捷方式.lnk");
            Pinning.Create(link,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"notepad.exe"),"","Test",Settings.Executable,"MSBC.Test");
            string url=Path.Combine(fixtures,"测试网页.url");
            File.WriteAllText(url,"[InternetShortcut]\r\nURL=https://example.com/\r\n");
            byte[] original=File.ReadAllBytes(link);
            manager.Add(capsule,new[]{link,url,link});
            Check(capsule.Items.Count==2,"batch import de-duplicates identical input paths");
            Check(File.ReadAllBytes(manager.Files.EntryPath(capsule,capsule.Items[0])).SequenceEqual(original),"real Shell .lnk copied byte-for-byte");
            manager.Open(capsule);
            Check(manager.Popup!=null&&manager.Popup.IsVisible,"real grid window opens");
            manager.Open(capsule);
            Check(manager.Popup==null,"second open toggles grid closed");
            manager.OpenById(capsule.Id);
            Check(manager.Popup!=null,"capsule ID route opens existing capsule");
            manager.ClosePopup();
            string pin=Pinning.Make(capsule);
            object com=new ShellLinkObject();
            try
            {
                ((IPersistFile)com).Load(pin,0);
                var target=new StringBuilder(1024);var arguments=new StringBuilder(256);
                ((IShellLink)com).GetPath(target,1024,IntPtr.Zero,0);
                ((IShellLink)com).GetArguments(arguments,256);
                Check(target.ToString()==Settings.Executable,"generated pin resolves to portable executable");
                Check(arguments.ToString()=="--sandbox --capsule "+capsule.Id,"sandbox pin cannot accidentally launch production mode");
            }
            finally { Marshal.FinalReleaseComObject(com); }
            manager.Launch(capsule,capsule.Items[0]);
            Check(File.ReadAllText(Path.Combine(Settings.DataDirectory,"actions.log")).Contains("LAUNCH simulated"),"launch records intent without starting target program");
            var entry=capsule.Items[0];
            string copied=manager.Files.EntryPath(capsule,entry);
            manager.DeleteEntries(capsule,new[]{entry});
            Check(!File.Exists(copied)&&File.ReadAllBytes(link).SequenceEqual(original)&&capsule.Items.Count==1,"entry deletion preserves original and simulates recycle");
            manager.Windows[capsule.Id].Left=-100000;
            manager.Windows[capsule.Id].Top=-100000;
            manager.Reposition();
            Check(manager.Windows[capsule.Id].Left>-100000&&manager.Windows[capsule.Id].Top>-100000,"off-screen capsule position clamped");
            Check(capsule.X==manager.Windows[capsule.Id].Left,"reposition updates saved model");
            foreach(var mode in new[]{"acrylic","clear","normal"})manager.SetTaskbarMode(mode);
            Check(Settings.Load().TaskbarMode=="normal"&&!Directory.Exists(Path.Combine(Settings.DataDirectory,"Runtime")),"three simulated taskbar modes persist without loading DLL");
            manager.RemoveCapsule(capsule);
            Check(!manager.Preferences.Capsules.Contains(capsule)&&!manager.Windows.ContainsKey(capsule.Id)&&File.Exists(url),"capsule deletion preserves original shortcuts");
            Check(Reject(()=>RuntimeMode.Recycle(Settings.Executable)),"sandbox recycle rejects paths outside data root");
            Check(File.Exists(Settings.Executable),"portable executable survives sandbox rejection");
            Check(manager.Preferences.Capsules.Count==Settings.Load().Capsules.Count,"saved capsule count round-trips");
            manager.Dispose();manager.Dispose();manager=null;
            Check(true,"window manager disposes idempotently");
            app.Dispatcher.Invoke(DispatcherPriority.Background,new Action(()=>{}));
            Check(!Directory.Exists(Path.Combine(Settings.InstallDirectory,"Data")),"integration run never created production Data");
            results.Add("TOTAL PASS "+count);Console.WriteLine("TOTAL PASS "+count);
        }
        catch(Exception ex){exitCode=1;Console.WriteLine(ex);results.Add(ex.ToString());}
        finally
        {
            if(manager!=null)manager.Dispose();
            File.WriteAllLines(Path.Combine(Settings.InstallDirectory,"portable-tests.log"),results,Encoding.UTF8);
            app.Shutdown();
        }
        return exitCode;
    }
}
