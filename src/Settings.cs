using System;
using System.Collections.Generic;
using System.IO;
namespace PocketGrid
{
    internal static class Settings
    {
        public static string InstallDirectory { get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar); } }
        public static string DataDirectory { get { return Path.Combine(InstallDirectory,RuntimeMode.Sandbox?"SandboxData":"Data"); } }
        public static string Executable { get { return Path.Combine(InstallDirectory,"MSBeatutyCapsule.exe"); } }
        public static string LoadNotice { get; private set; }
        public static bool FreshStart { get; private set; }
        internal static string Snapshot { get { return Path.Combine(DataDirectory,"Config","configuration.xml"); } }
        public static Preferences Load()
        {
            LoadNotice=null;
            InstallFiles.Normalize(DataDirectory);
            Directory.CreateDirectory(DataDirectory);
            var candidates=new List<ConfigCandidate>();
            foreach(string suffix in new[]{"",".bak",".pending"})
            {
                string file=Snapshot+suffix;
                if(File.Exists(file)) candidates.Add(new ConfigCandidate("disk"+suffix,File.ReadAllText(file)));
            }
            var result=Recovery.Select(candidates,Path.Combine(DataDirectory,"Capsules"));
            FreshStart=result.Source=="new"&&!result.Recovered;
            if(result.Recovered)
                LoadNotice=result.Source=="files"?"配置和备份不可用，已从快捷方式副本重建胶囊。部分名称使用临时名称。":result.Source=="new"?"配置及备份损坏，未找到可恢复的副本，已创建空胶囊。损坏文件将在保存时保留。":"已从本地备份恢复胶囊配置。";
            return result.Value;
        }
        public static void Save(Preferences p)
        {
            InstallFiles.Normalize(DataDirectory);
            InstallFiles.Normalize(Path.GetDirectoryName(Snapshot));
            long previous=p.Revision;
            p.Revision=checked(previous+1);
            try { Recovery.WriteSnapshot(Snapshot,Logic.Serialize(p)); }
            catch { p.Revision=previous; throw; }
        }
        static StartupRegistration Registration()
        {
            IStartupStore store=RuntimeMode.Sandbox
                ? (IStartupStore)new FileStartupStore(Path.Combine(DataDirectory,"SimulatedSystem","startup.txt"))
                : new ShortcutStartupStore(StartupLink,Executable);
            return new StartupRegistration(store,Executable);
        }
        public static bool StartupEnabled() { return Registration().Enabled; }
        public static string StartupLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"MS Beatuty Capsule Portable.lnk"); } }
        public static void SetStartup(bool enabled)
        {
            if(RuntimeMode.Sandbox)Registration().Set(enabled);
            else StartupSetup.Apply(new ShortcutStartupStore(StartupLink,Executable),new RegistryStartupStore(),Executable,enabled);
            StartupLog.Write("registration enabled="+enabled+" verified="+StartupEnabled());
        }
    }
}
