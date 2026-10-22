using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Win32;
namespace PocketGrid
{
    internal interface IStartupStore { string Read(); void Write(string command); void Delete(); }
    internal sealed class RegistryStartupStore : IStartupStore
    {
        const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name="MS Beatuty Capsule Portable";
        public string Read() { using(var key=Registry.CurrentUser.OpenSubKey(Key)) return key==null?null:key.GetValue(Name) as string; }
        public void Write(string command) { using(var key=Registry.CurrentUser.CreateSubKey(Key)) key.SetValue(Name,command,RegistryValueKind.String); }
        public void Delete() { using(var key=Registry.CurrentUser.OpenSubKey(Key,true)) if(key!=null) key.DeleteValue(Name,false); }
    }
    internal sealed class FileStartupStore : IStartupStore
    {
        readonly string path;
        public FileStartupStore(string path) { this.path=path; }
        public string Read() { return File.Exists(path)?File.ReadAllText(path):null; }
        public void Write(string command) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path,command); }
        public void Delete() { if(File.Exists(path)) File.Delete(path); }
    }
    internal sealed class StartupRegistration
    {
        readonly IStartupStore store;
        public readonly string Command;
        public StartupRegistration(IStartupStore store,string executable)
        {
            this.store=store;
            if(!Path.IsPathRooted(executable)||executable.IndexOfAny(new[]{'"','\r','\n'})>=0) throw new IOException("启动路径无效。");
            Command="\""+Path.GetFullPath(executable)+"\" --background";
        }
        public bool Enabled { get { return String.Equals(store.Read(),Command,StringComparison.OrdinalIgnoreCase); } }
        public void Set(bool enabled)
        {
            if(enabled)
            {
                if(Command.Length>260) throw new IOException("路径过长，Windows 自启命令不能超过 260 个字符。请将整个便携文件夹移到更短的路径。");
                store.Write(Command);
                if(!Enabled)throw new IOException("自启登记后读回不一致，尚未成功启用。");
            }
            else if(Enabled)
            {
                store.Delete(); // Do not delete another portable copy's entry.
                if(Enabled)throw new IOException("自启快捷方式未能清理，请检查启动文件夹权限。");
            }
        }
    }

    internal sealed class ShortcutStartupStore : IStartupStore
    {
        readonly string path, executable;
        public ShortcutStartupStore(string path,string executable)
        {
            this.path=Path.GetFullPath(path);
            this.executable=Path.GetFullPath(executable);
        }
        public static string ReadLink(string path)
        {
            if(!File.Exists(path))return null;
            object instance=new ShellLinkObject();
            try
            {
                ((IPersistFile)instance).Load(path,0);
                var link=(IShellLink)instance;
                var target=new StringBuilder(32768);var args=new StringBuilder(32768);
                link.GetPath(target,target.Capacity,IntPtr.Zero,4);
                link.GetArguments(args,args.Capacity);
                return "\""+target+"\" "+args;
            }
            finally{Marshal.FinalReleaseComObject(instance);}
        }
        public string Read(){return ReadLink(path);}
        public void Write(string command)
        {
            if(!String.Equals(command,"\""+executable+"\" --background",StringComparison.OrdinalIgnoreCase))throw new IOException("自启命令与程序路径不一致。");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".lnk";
            try
            {
                Pinning.Create(temporary,executable,"--background","MS Beatuty Capsule Portable",executable,"MS.BeatutyCapsule");
                if(!String.Equals(ReadLink(temporary),command,StringComparison.OrdinalIgnoreCase))throw new IOException("自启快捷方式核验失败。");
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        public void Delete(){if(File.Exists(path))File.Delete(path);}
    }
    internal static class StartupSetup
    {
        public static void Apply(IStartupStore primary,IStartupStore legacy,string executable,bool enabled)
        {
            var current=new StartupRegistration(primary,executable);
            if(!enabled||!current.Enabled)current.Set(enabled);
            // Retire only this copy's old Run value, after the shortcut has been verified.
            new StartupRegistration(legacy,executable).Set(false);
        }
    }
}
