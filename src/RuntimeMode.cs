using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace PocketGrid
{
    internal static class RuntimeMode
    {
        public static bool Sandbox { get; private set; }
        public static void Initialize(string[] args) { Sandbox=args.Contains("--sandbox"); }
        public static string Identity
        {
            get
            {
                if(!Sandbox) return "MS.BeatutyCapsule.Portable";
                using(var sha=SHA256.Create()) return "MS.BeatutyCapsule.Sandbox."+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Settings.InstallDirectory.ToUpperInvariant()))).Replace("-","").Substring(0,16);
            }
        }
        public static string ControllerTitle { get { return Identity+".Controller"; } }
        public static void Log(string action)
        {
            if(!Sandbox) return;
            Directory.CreateDirectory(Settings.DataDirectory);
            File.AppendAllText(Path.Combine(Settings.DataDirectory,"actions.log"),DateTime.UtcNow.ToString("o")+" "+action+Environment.NewLine);
        }
        public static void Recycle(string path)
        {
            if(!Sandbox) { Native.Recycle(path); return; }
            string full=Path.GetFullPath(path);
            string root=Path.GetFullPath(Settings.DataDirectory)+Path.DirectorySeparatorChar;
            if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)) throw new IOException("沙箱拒绝操作外部路径。");
            string trash=Path.Combine(Settings.DataDirectory,"SimulatedRecycleBin");
            Directory.CreateDirectory(trash);
            string target=Path.Combine(trash,Guid.NewGuid().ToString("N")+"-"+Path.GetFileName(full));
            if(Directory.Exists(full)) Directory.Move(full,target); else File.Move(full,target);
            Log("RECYCLE "+full);
        }
    }
}