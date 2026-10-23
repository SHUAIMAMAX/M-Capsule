using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace PocketGrid
{
    public static class InstallFiles
    {
        public const string Product="M-Capsule";
        public const string Marker=".msbc-install";
        public static readonly string[] OwnedFiles={"M-Capsule.exe","M-Capsule.exe.config","M-Capsule-Source.zip","LICENSE.txt","THIRD-PARTY-NOTICES.txt","使用说明.txt","vendor/ExplorerTAP.dll","vendor/Detours-LICENSE.txt","vendor/WIL-LICENSE.txt",Marker};
        public static string Normalize(string path)
        {
            if(String.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path))throw new IOException("请选择完整的安装路径。");
            string full=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            string root=Path.GetPathRoot(Path.GetFullPath(path)).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(String.Equals(full,root,StringComparison.OrdinalIgnoreCase))throw new IOException("请选择磁盘内的专用子文件夹，不能直接安装到磁盘根目录。");
            for(var current=new DirectoryInfo(full);current!=null;current=current.Parent)
                if(current.Exists&&(current.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("安装路径不能经过符号链接或目录联接。");
            return full;
        }
        public static void CopyData(string source,string destination)
        {
            source=Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);destination=Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar);
            if(String.Equals(source,destination,StringComparison.OrdinalIgnoreCase)||!Directory.Exists(source))return;
            if(destination.StartsWith(source+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||source.StartsWith(destination+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("新旧数据目录不能互相包含。");
            if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)throw new IOException("数据来源不能是目录联接。");
            // Only authoritative data is migrated. Caches are rebuilt in the new installation.
            foreach(string name in new[]{"Capsules","Config"})
            {
                string from=Path.Combine(source,name);if(Directory.Exists(from))CopyTree(from,Path.Combine(destination,name));
            }
        }
        static void CopyTree(string source,string destination)
        {
            if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)throw new IOException("数据迁移遇到目录联接，已停止。");
            if(Directory.Exists(destination)&&(File.GetAttributes(destination)&FileAttributes.ReparsePoint)!=0)throw new IOException("目标数据目录不能是目录联接。");
            Directory.CreateDirectory(destination);
            foreach(string file in Directory.GetFiles(source))
            {
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("数据迁移遇到符号链接，已停止。");
                string target=Path.Combine(destination,Path.GetFileName(file));
                if(File.Exists(target))
                {
                    if(!SameBytes(file,target))throw new IOException("目标位置存在不同版本的数据："+target+"。请选择新的空文件夹。");
                }
                else File.Copy(file,target);
                if(!SameBytes(file,target))throw new IOException("迁移后的数据校验失败："+target);
            }
            foreach(string dir in Directory.GetDirectories(source))CopyTree(dir,Path.Combine(destination,Path.GetFileName(dir)));
        }
        static bool SameBytes(string a,string b)
        {
            using(var hash=SHA256.Create())using(var first=File.OpenRead(a))using(var second=File.OpenRead(b))return hash.ComputeHash(first).SequenceEqual(hash.ComputeHash(second));
        }
        public static void DeleteTree(string directory)
        {
            if(!Directory.Exists(directory))return;
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0){Directory.Delete(directory);return;}
            foreach(string file in Directory.GetFiles(directory))
            {
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)==0)File.SetAttributes(file,File.GetAttributes(file)&~FileAttributes.ReadOnly);File.Delete(file);
            }
            foreach(string child in Directory.GetDirectories(directory))DeleteTree(child);
            Directory.Delete(directory);
        }
        public static List<string> DeleteOwnedData(string root)
        {
            var remaining=new List<string>();
            if(Directory.Exists(root)&&(File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0){Directory.Delete(root);return remaining;}
            // Delete shortcuts first; a locked Explorer DLL must not prevent their removal.
            foreach(string name in new[]{"Capsules","Pins","Config","Cache","Runtime","Uninstall"})
            {
                string path=Path.Combine(root,name);
                try{DeleteTree(path);}catch(IOException){remaining.Add(path);}catch(UnauthorizedAccessException){remaining.Add(path);}
            }
            try{if(Directory.Exists(root)&&!Directory.EnumerateFileSystemEntries(root).Any())Directory.Delete(root);}catch{}
            return remaining;
        }
    }
}
