using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PocketGrid
{
    public sealed class ConfigCandidate
    {
        public string Name, Text;
        public ConfigCandidate(string name,string text){Name=name;Text=text;}
    }
    public sealed class RecoveryResult
    {
        public Preferences Value;
        public string Source;
        public bool Recovered;
    }
    public static class Recovery
    {
        public static bool Valid(string xml)
        {
            if(String.IsNullOrEmpty(xml))return false;
            try{Logic.Deserialize(xml);return true;}catch{return false;}
        }
        public static RecoveryResult Select(IEnumerable<ConfigCandidate> candidates,string capsuleRoot)
        {
            Preferences best=null;string source=null;bool hadData=false;
            foreach(var candidate in candidates)
            {
                if(String.IsNullOrEmpty(candidate.Text))continue;
                hadData=true;
                try
                {
                    var value=Logic.Deserialize(candidate.Text);
                    if(best==null||value.Revision>best.Revision){best=value;source=candidate.Name;}
                }
                catch(InvalidOperationException){}catch(InvalidDataException){}catch(System.Xml.XmlException){}
            }
            if(best!=null)return new RecoveryResult{Value=best,Source=source,Recovered=source!="registry"&&source!="disk"};
            var rebuilt=Rebuild(capsuleRoot);
            if(rebuilt.Capsules.Count>0)return new RecoveryResult{Value=rebuilt,Source="files",Recovered=true};
            var fresh=new Preferences{NextColor=1};fresh.Capsules.Add(new Capsule{ColorIndex=0});
            return new RecoveryResult{Value=fresh,Source="new",Recovered=hadData};
        }
        public static Preferences Rebuild(string root)
        {
            var p=new Preferences();
            if(!Directory.Exists(root))return p;
            if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new IOException("胶囊目录不能是目录联接。");
            foreach(string dir in Directory.GetDirectories(root).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase))
            {
                if(!Logic.IsId(Path.GetFileName(dir))||(File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)continue;
                if(p.Capsules.Count>=Logic.MaxCapsules)break;
                var c=new Capsule{Id=Path.GetFileName(dir),Name="恢复的胶囊 "+(p.Capsules.Count+1),ColorIndex=p.Capsules.Count,X=32+(p.Capsules.Count%6)*92,Y=140+(p.Capsules.Count/6)*110};
                foreach(string file in Directory.GetFiles(dir).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase))
                {
                    string id=Path.GetFileNameWithoutExtension(file);
                    if(!Logic.IsId(id)||!Logic.IsShortcut(file)||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)continue;
                    if(c.Items.Count>=Logic.MaxItems)break;
                    if(c.Items.Any(e=>e.Id==id))continue;
                    c.Items.Add(new Shortcut{Id=id,Extension=Path.GetExtension(file).ToLowerInvariant(),Name="恢复的快捷方式 "+(c.Items.Count+1)});
                }
                foreach(string entryDir in Directory.GetDirectories(dir).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase))
                {
                    string id=Path.GetFileName(entryDir);
                    if(c.Items.Count>=Logic.MaxItems)break;
                    if(!Logic.IsId(id)||(File.GetAttributes(entryDir)&FileAttributes.ReparsePoint)!=0)continue;
                    // A process interruption during atomic reimport can leave a staging file
                    // beside the previous valid copy. Recover the valid copy in that case.
                    var files=Directory.GetFiles(entryDir).Where(file=>!IsCopyStaging(file)).ToArray();
                    if(files.Length!=1||(File.GetAttributes(files[0])&FileAttributes.ReparsePoint)!=0)continue;
                    string name=Path.GetFileName(files[0]);
                    try{ShortcutStore.ValidateStoredName(name);}catch(InvalidDataException){continue;}
                    c.Items.Add(new Shortcut{Id=id,Extension=".copy",StoredName=name,Name=Logic.IsShortcut(name)?Path.GetFileNameWithoutExtension(name):name});
                }
                p.Capsules.Add(c);
            }
            p.NextColor=p.Capsules.Count;return p;
        }
        static bool IsCopyStaging(string path)
        {
            string name=Path.GetFileName(path);
            if(name.EndsWith(".pending",StringComparison.OrdinalIgnoreCase))return true;
            return name.Length==9&&name[0]=='~'&&name.Skip(1).All(c=>
                (c>='0'&&c<='9')||(c>='a'&&c<='f')||(c>='A'&&c<='F'));
        }
        public static void WriteSnapshot(string file,string xml)
        {
            // Fully validate before replacing anything. Keep the previous VALID snapshot.
            Logic.Deserialize(xml);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string temporary=file+".pending";
            using(var output=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            {
                byte[] bytes=new UTF8Encoding(false).GetBytes(xml);output.Write(bytes,0,bytes.Length);output.Flush(true);
            }
            if(File.Exists(file))
            {
                string old=File.ReadAllText(file);
                if(Valid(old))File.Replace(temporary,file,file+".bak");
                else
                {
                    File.Copy(file,file+".damaged-"+Guid.NewGuid().ToString("N"));
                    File.Replace(temporary,file,null);
                }
            }
            else File.Move(temporary,file);
        }
    }
}
