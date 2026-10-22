using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using PocketGrid;

public static class ReferenceTests
{
    public static void Main()
    {
        int count=0;
        Action<bool,string> check=(ok,name)=>{count++;if(!ok)throw new Exception(name);};
        Func<Action,bool> reject=action=>{try{action();return false;}catch{return true;}};
        string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"reference-tests-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store=new ShortcutStore(Path.Combine(root,"managed"));var c=new Capsule();
            foreach(string ext in new[]{".jpg",".png",".heic",".raw",".pdf",".docx",".xlsx",".pptx",".mp4",".zip",".7z",".exe",".lnk",".url",".unknown",""})
            {
                string path=Path.Combine(root,"中文 & 测试"+ext);File.WriteAllText(path,"original bytes "+ext);
                // Exclusive handle prevents reading/copying file content. Mapping still succeeds.
                using(var locked=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                {
                    var entry=store.AddReference(c,path);
                    check(entry.IsReference&&store.EntryPath(c,entry)==path,"reference without reading content "+ext);
                    check(store.AddReference(c,path)==entry,"deduplicate "+ext);
                }
            }
            string directory=Path.Combine(root,"文件夹");Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"keep.txt"),"keep");
            var folder=store.AddReference(c,directory);
            check(store.EntryPath(c,folder)==directory,"folder reference");
            check(!Directory.Exists(store.Root),"no imported files or per-entry sidecars written");
            var p=new Preferences{TaskbarMode="clear",StartWithWindows=true};p.Capsules.Add(c);
            var loaded=Logic.Deserialize(Logic.Serialize(p));
            check(loaded.Version==3&&loaded.Capsules[0].Items.Count==17,"reference config roundtrip");
            check(loaded.StartWithWindows&&loaded.TaskbarMode=="clear","startup preference survives");
            check(loaded.Capsules[0].Items.All(e=>e.TargetPath==c.Items.First(x=>x.Id==e.Id).TargetPath),"all absolute targets preserved");
            var missing=c.Items[0];File.Delete(missing.TargetPath);
            check(Logic.Deserialize(Logic.Serialize(p)).Capsules[0].Items.Count==17,"missing source retained on reload");
            int recycled=0;
            store.Remove(c,c.Items.ToArray(),path=>{recycled++;throw new Exception("must not recycle original");});
            check(recycled==0&&c.Items.Count==0,"delete references never recycles source");
            check(File.ReadAllText(Path.Combine(directory,"keep.txt"))=="keep","folder content untouched");
            store.AddReference(c,directory);store.RemoveCapsule(c,path=>{throw new Exception("no owned directory");});
            check(Directory.Exists(directory),"delete capsule leaves referenced folder");
            foreach(string invalid in new[]{"relative.txt",@"C:relative.txt","https://example.com",@"\\.\pipe\x",@"\\?\C:\x",@"C:\file.txt:stream"})
                check(reject(()=>ShortcutStore.ValidateTarget(invalid)),"reject non-filesystem target "+invalid);
            check(ShortcutStore.ValidateTarget(@"\\server\share\photo.jpg")==@"\\server\share\photo.jpg","UNC path supported without probing on deserialize");
            string legacyFile=Path.Combine(root,"legacy.lnk");File.WriteAllText(legacyFile,"legacy");
            var legacy=new Capsule();var copy=store.CopyIn(legacy,legacyFile);
            var old=new Preferences{Version=2};old.Capsules.Add(legacy);
            var upgraded=Logic.Deserialize(Logic.Serialize(old));
            check(upgraded.Version==3&&!upgraded.Capsules[0].Items[0].IsReference,"v2 migration retains owned shortcut mode");
            check(File.ReadAllText(store.EntryPath(legacy,copy))=="legacy","old copied shortcut usable");
            store.Remove(legacy,new[]{copy},File.Delete);
            check(File.Exists(legacyFile),"legacy delete still preserves original");
            var many=new Capsule();var watch=Stopwatch.StartNew();
            for(int i=0;i<512;i++){string path=Path.Combine(root,"item"+i);File.WriteAllText(path,"x");store.AddReference(many,path);}
            watch.Stop();check(many.Items.Count==512,"512 mappings");
            check(reject(()=>store.AddReference(many,legacyFile)),"capacity enforced");
            var data=new Preferences();data.Capsules.Add(many);
            watch.Restart();string xml=Logic.Serialize(data);var round=Logic.Deserialize(xml);watch.Stop();
            check(round.Capsules[0].Items.Count==512,"512 mappings reload");
            Console.WriteLine("512-entry XML roundtrip: "+watch.ElapsedMilliseconds+" ms; chars="+xml.Length+" (fixture benchmark, not login timing)");
            Console.WriteLine("PASS: "+count+" reference checks.");
        }
        finally { Directory.Delete(root,true); }
    }
}
