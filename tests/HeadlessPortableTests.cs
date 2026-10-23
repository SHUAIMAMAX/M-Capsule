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

public static class HeadlessPortableTests
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


        int exitCode=0;
        try
        {
            Check(RuntimeMode.Sandbox&&Settings.DataDirectory.EndsWith("SandboxData"),"test data uses isolated directory");
            Check(RuntimeMode.Identity.Contains("Sandbox"),"sandbox IPC identity differs from production");
            var empty=Settings.Load();
            Check(empty.Capsules.Count==1&&empty.StartWithWindows&&Settings.LoadNotice==null,"fresh portable configuration defaults to autostart");
            Settings.Save(empty);
            var reloaded=Settings.Load();
            Check(reloaded.Capsules[0].Id==empty.Capsules[0].Id&&reloaded.StartWithWindows&&Settings.LoadNotice==null,"healthy disk reload preserves autostart preference");
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
            Check(!startup.Enabled,"new registration has no Run value before first app launch");
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

            RuntimeMode.Initialize(new string[0]);
            Check(Settings.DataDirectory.EndsWith(Path.DirectorySeparatorChar+"Data"),"normal mode uses executable-relative Data");
            var production=Settings.Load();
            Check(production.Capsules.Count==1&&Settings.LoadNotice==null,"normal mode does not inherit sandbox configuration");
            production.Capsules[0].Name="Portable local data";
            Settings.Save(production);
            Check(Settings.Load().Capsules[0].Name=="Portable local data","normal disk-only configuration persists");
            var store=new ShortcutStore(Path.Combine(Settings.DataDirectory,"Capsules"));
            string fixture=Path.Combine(Settings.DataDirectory,"original.url");
            File.WriteAllText(fixture,"[InternetShortcut]\r\nURL=https://example.com/\r\n");
            var item=store.CopyIn(production.Capsules[0],fixture);Settings.Save(production);
            string moved=Path.Combine(Settings.InstallDirectory,"Moved copy 中文","Data");
            InstallFiles.CopyData(Settings.DataDirectory,moved);
            var movedConfig=Logic.Deserialize(File.ReadAllText(Path.Combine(moved,"Config","configuration.xml")));
            var movedStore=new ShortcutStore(Path.Combine(moved,"Capsules"));
            Check(File.Exists(movedStore.EntryPath(movedConfig.Capsules[0],movedConfig.Capsules[0].Items[0])),"moving folder preserves relative shortcut references");
            Check(!File.ReadAllText(Path.Combine(moved,"Config","configuration.xml")).Contains(Settings.InstallDirectory),"configuration contains no absolute installation path");
            Check(File.Exists(fixture),"folder migration preserves original source");
            RuntimeMode.Initialize(new[]{"--sandbox"});
            Check(Settings.Load().Capsules[0].Name!="Portable local data","sandbox remains independent after normal-mode save");
            results.Add("TOTAL PASS "+count);Console.WriteLine("TOTAL PASS "+count);
        }
        catch(Exception ex){exitCode=1;Console.WriteLine(ex);results.Add(ex.ToString());}
        finally { File.WriteAllLines(Path.Combine(Settings.InstallDirectory,"headless-tests.log"),results,Encoding.UTF8); }
        return exitCode;
    }
}
