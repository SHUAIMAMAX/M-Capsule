using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using PocketGrid;

public static class StartupTests
{
    static int count;
    static void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);count++;Console.WriteLine("PASS "+name);}
    sealed class MemoryStore:IStartupStore
    {
        public string Value;public int Writes;public bool IgnoreWrite,FailWrite,IgnoreDelete;
        public string Read(){return Value;}
        public void Write(string value){Writes++;if(FailWrite)throw new IOException("blocked");if(!IgnoreWrite)Value=value;}
        public void Delete(){if(!IgnoreDelete)Value=null;}
    }
    [STAThread] public static int Main()
    {
        RuntimeMode.Initialize(new[]{"--sandbox"});
        try
        {
            string executable=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"中文 & space","M-Capsule.exe");
            string command="\""+executable+"\" --background";
            var primary=new MemoryStore();var legacy=new MemoryStore{Value=command};
            StartupSetup.Apply(primary,legacy,executable,true);
            Check(primary.Value==command&&legacy.Value==null,"verified shortcut replaces this copy's old Run value");
            StartupSetup.Apply(primary,legacy,executable,true);
            Check(primary.Writes==1,"healthy startup is not rewritten on every launch");
            legacy.Value="another copy";StartupSetup.Apply(primary,legacy,executable,false);
            Check(primary.Value==null&&legacy.Value=="another copy","disable removes only owned entries");
            primary.Value=command;primary.IgnoreDelete=true;
            bool deleteRejected=false;try{new StartupRegistration(primary,executable).Set(false);}catch(IOException){deleteRejected=true;}
            Check(deleteRejected&&primary.Value==command,"failed startup cleanup is reported and keeps application running");
            primary.IgnoreDelete=false;new StartupRegistration(primary,executable).Set(false);
            primary.FailWrite=true;legacy.Value=command;
            try{StartupSetup.Apply(primary,legacy,executable,true);}catch(IOException){}
            Check(legacy.Value==command,"failed shortcut creation preserves old Run value");
            primary.FailWrite=false;primary.IgnoreWrite=true;
            bool rejected=false;try{StartupSetup.Apply(primary,legacy,executable,true);}catch(IOException){rejected=true;}
            Check(rejected&&legacy.Value==command,"write readback mismatch is rejected before legacy cleanup");

            string fixture=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-fixture-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.GetDirectoryName(executable));File.Copy(typeof(StartupTests).Assembly.Location,executable);
            string linkPath=Path.Combine(fixture,"startup.lnk");
            var store=new ShortcutStartupStore(linkPath,executable);var registration=new StartupRegistration(store,executable);
            registration.Set(true);
            Check(registration.Enabled,"real shell shortcut target and background arguments read back correctly");
            object instance=new ShellLinkObject();
            try
            {
                ((IPersistFile)instance).Load(linkPath,0);var shell=(IShellLink)instance;
                var directory=new StringBuilder(32768);shell.GetWorkingDirectory(directory,directory.Capacity);
                Check(directory.ToString()==Path.GetDirectoryName(executable),"shortcut working directory points to portable folder");
            }
            finally{Marshal.FinalReleaseComObject(instance);}
            registration.Set(true);Check(registration.Enabled,"atomic replacement keeps shortcut valid");
            Check(Directory.GetFiles(fixture).Length==1,"no temporary shortcut remains after write");
            var foreign=new StartupRegistration(store,Path.Combine(fixture,"other.exe"));foreign.Set(false);
            Check(File.Exists(linkPath),"another copy cannot delete this shortcut");
            registration.Set(false);Check(!File.Exists(linkPath),"disable removes the owned fixture shortcut");
            string oldExecutable=Path.Combine(Path.GetDirectoryName(executable),"MSBeatutyCapsule.exe");
            File.Copy(typeof(StartupTests).Assembly.Location,oldExecutable,true);
            string oldLink=Path.Combine(fixture,"MS Beatuty Capsule Portable.lnk");
            new StartupRegistration(new ShortcutStartupStore(oldLink,oldExecutable),oldExecutable).Set(true);
            var oldRun=new MemoryStore{Value="\""+oldExecutable+"\" --background"};
            StartupSetup.RetireOldName(oldRun,oldExecutable,oldLink);
            Check(oldRun.Value==null&&!File.Exists(oldLink),"rename removes this installation's old shortcut and Run entry");
            string foreignOld=Path.Combine(fixture,"foreign-old.exe");
            new StartupRegistration(new ShortcutStartupStore(oldLink,foreignOld),foreignOld).Set(true);
            StartupSetup.RetireOldName(new MemoryStore(),oldExecutable,oldLink);
            Check(File.Exists(oldLink),"rename preserves another installation's old shortcut");
            File.WriteAllText(oldLink,"damaged shortcut fixture");
            StartupSetup.RetireOldName(new MemoryStore(),oldExecutable,oldLink);
            Check(File.Exists(oldLink),"damaged old shortcut cannot block new startup or be deleted as owned");
            Console.WriteLine("TOTAL PASS "+count+"; shortcuts created only inside test directory; no real startup modified.");return 0;
        }
        catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
}
