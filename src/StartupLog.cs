using System;
using System.IO;
using System.Reflection;
namespace PocketGrid
{
    internal static class StartupLog
    {
        public static void Write(string message)
        {
            try
            {
                string directory=Path.Combine(Settings.DataDirectory,"Logs");
                Directory.CreateDirectory(directory);
                string path=Path.Combine(directory,"startup.log");
                if(File.Exists(path)&&new FileInfo(path).Length>512*1024)File.Copy(path,path+".previous",true);
                if(File.Exists(path)&&new FileInfo(path).Length>512*1024)File.Delete(path);
                File.AppendAllText(path,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+" v"+Assembly.GetExecutingAssembly().GetName().Version+" "+message+Environment.NewLine);
            }
            catch { /* Logging must never stop startup or reset configuration. */ }
        }
    }
}
