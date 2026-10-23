using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PocketGrid
{
    public sealed class OrganizePlan
    {
        public string DesktopDirectory { get; internal set; }
        public string Destination { get; internal set; }
        public bool DestinationExists { get; internal set; }
        public string[] Files { get; internal set; }
    }
    public static class UltimateOrganizer
    {
        public static OrganizePlan Prepare(string desktop)
        {
            if(String.IsNullOrWhiteSpace(desktop))throw new IOException("当前用户桌面目录不可用。");
            string root=Path.GetFullPath(desktop);
            if(!Directory.Exists(root)||String.Equals(root,Path.GetPathRoot(root),StringComparison.OrdinalIgnoreCase))
                throw new IOException("当前用户桌面目录不可用。");
            string destination=Path.Combine(root,"胶囊整理");
            if(File.Exists(destination))throw new IOException("桌面已有同名文件“胶囊整理”，请先自行确认后再使用此功能。");
            var files=Directory.GetFiles(root).Where(path=>
            {
                string name=Path.GetFileName(path);
                var attributes=File.GetAttributes(path);
                return !String.Equals(name,"desktop.ini",StringComparison.OrdinalIgnoreCase)&&
                    (attributes&(FileAttributes.Hidden|FileAttributes.System))==0;
            }).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
            return new OrganizePlan{DesktopDirectory=root,Destination=destination,DestinationExists=Directory.Exists(destination),Files=files};
        }
        static string AvailableTarget(string destination,string name,HashSet<string> reserved)
        {
            string stem=Path.GetFileNameWithoutExtension(name),extension=Path.GetExtension(name);
            for(int suffix=1;suffix<10001;suffix++)
            {
                string candidate=suffix==1?name:stem+" ("+suffix+")"+extension;
                string path=Path.Combine(destination,candidate);
                if(!reserved.Contains(path)&&!File.Exists(path)&&!Directory.Exists(path))
                {
                    reserved.Add(path);
                    return path;
                }
            }
            throw new IOException("整理文件夹中的同名文件过多，请手动处理后重试："+name);
        }
        public static string Execute(OrganizePlan plan,bool approved,Action<int> beforeMove=null)
        {
            if(!approved)return null;
            if(plan==null||plan.Files==null||String.IsNullOrWhiteSpace(plan.DesktopDirectory))throw new ArgumentException("整理计划无效。");
            string root=Path.GetFullPath(plan.DesktopDirectory);
            string destination=Path.Combine(root,"胶囊整理");
            if(!String.Equals(destination,plan.Destination,StringComparison.OrdinalIgnoreCase)||File.Exists(destination)||
               Directory.Exists(destination)!=plan.DestinationExists)throw new IOException("整理目标文件夹已改变，请重新执行整理。");
            foreach(string file in plan.Files)
                if(!String.Equals(Path.GetDirectoryName(Path.GetFullPath(file)),root,StringComparison.OrdinalIgnoreCase)||
                   !File.Exists(file)||Path.GetFileName(file)=="desktop.ini")throw new IOException("桌面文件已改变，请重新执行整理。");
            var reserved=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var targets=plan.Files.Select(file=>AvailableTarget(destination,Path.GetFileName(file),reserved)).ToArray();
            if(!plan.DestinationExists)Directory.CreateDirectory(destination);
            var moved=new List<Tuple<string,string>>();
            try
            {
                for(int i=0;i<plan.Files.Length;i++)
                {
                    if(beforeMove!=null)beforeMove(i);
                    string source=plan.Files[i],target=targets[i];
                    File.Move(source,target);
                    moved.Add(Tuple.Create(source,target));
                }
                return destination;
            }
            catch(Exception original)
            {
                var failed=new List<string>();
                for(int i=moved.Count-1;i>=0;i--)
                {
                    string source=moved[i].Item1,target=moved[i].Item2;
                    try{if(File.Exists(target)&&!File.Exists(source))File.Move(target,source);else failed.Add(source);}
                    catch{failed.Add(source);}
                }
                if(failed.Count==0&&!plan.DestinationExists)Directory.Delete(destination,false);
                else throw new IOException("整理中断且部分文件未能回退，请检查“胶囊整理”："+String.Join("、",failed.Select(Path.GetFileName).Take(8)),original);
                throw new IOException("整理中断，已将文件移回原位："+original.Message,original);
            }
        }
    }
}
