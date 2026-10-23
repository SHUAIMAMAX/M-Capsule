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
            check(loaded.Version==4&&loaded.Capsules[0].Items.Count==17,"reference config roundtrip");
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
            check(upgraded.Version==4&&!upgraded.Capsules[0].Items[0].IsReference,"v2 migration retains owned shortcut mode");
            check(File.ReadAllText(store.EntryPath(legacy,copy))=="legacy","old copied shortcut usable");
            store.Remove(legacy,new[]{copy},File.Delete);
            check(File.Exists(legacyFile),"legacy delete still preserves original");
            var imported=new Capsule();
            string small=Path.Combine(root,"photo.jpg");File.WriteAllText(small,"a photo");
            var photo=store.Add(imported,small);
            check(photo.Extension==".copy"&&File.ReadAllText(store.EntryPath(imported,photo))=="a photo","small photo copied into capsule");
            check(store.Add(imported,small)==photo,"small import deduplicates");
            File.Delete(small);
            check(File.ReadAllText(store.EntryPath(imported,photo))=="a photo","copy survives original deletion");
            string shortcut=Path.Combine(root,"Desktop.lnk");File.WriteAllText(shortcut,"shortcut bytes");
            var ownedShortcut=store.Add(imported,shortcut);File.Delete(shortcut);
            check(ownedShortcut.Extension==".copy"&&File.ReadAllText(store.EntryPath(imported,ownedShortcut))=="shortcut bytes","shortcut survives original deletion");
            string boundary=Path.Combine(root,"boundary.bin");using(var stream=File.Create(boundary))stream.SetLength(ShortcutStore.CopyLimitBytes);
            var atLimit=store.Add(imported,boundary);check(atLimit.Extension==".copy"&&new FileInfo(store.EntryPath(imported,atLimit)).Length==ShortcutStore.CopyLimitBytes,"exactly 5 MiB is copied");
            string large=Path.Combine(root,"large.bin");using(var stream=File.Create(large))stream.SetLength(ShortcutStore.CopyLimitBytes+1);
            var mapped=store.Add(imported,large);check(mapped.Extension==".copy"&&new FileInfo(store.EntryPath(imported,mapped)).Length==ShortcutStore.CopyLimitBytes+1,"over 5 MiB is copied without a size cutoff");
            var loadedCopies=new Preferences();loadedCopies.Capsules.Add(imported);var copiesRoundtrip=Logic.Deserialize(Logic.Serialize(loadedCopies));
            check(File.Exists(store.EntryPath(copiesRoundtrip.Capsules[0],copiesRoundtrip.Capsules[0].Items[0])),"copied file survives configuration reload");
            check(reject(()=>Logic.Deserialize(Logic.Serialize(loadedCopies).Replace("<StoredName>photo.jpg</StoredName>","<StoredName>..\\escape.jpg</StoredName>"))),"reject copied-file traversal");
            var recoveredCopies=Recovery.Rebuild(store.Root);
            check(recoveredCopies.Capsules.Any(x=>x.Id==imported.Id&&x.Items.Any(y=>y.Extension==".copy"&&y.StoredName=="photo.jpg")),"recover copied file from storage");
            int ownedRecycled=0;store.Remove(imported,new[]{photo,ownedShortcut},path=>{ownedRecycled++;Directory.Delete(path,true);});
            check(ownedRecycled==2&&File.Exists(large),"removing copies never deletes source");
            string copyOut=Path.Combine(root,"copy-out.bin");File.Copy(store.EntryPath(imported,mapped),copyOut);
            store.RemoveTransferred(imported,new[]{mapped},path=>Directory.Delete(path,true));
            check(File.Exists(copyOut)&&File.Exists(large)&&!imported.Items.Contains(mapped),"successful drag-copy removes capsule copy and preserves exported file and original");
            string moveSource=Path.Combine(root,"move-source.exe");File.WriteAllText(moveSource,"program bytes");
            var moveEntry=store.Add(imported,moveSource);string movedOut=Path.Combine(root,"moved-out.exe");
            File.Move(store.EntryPath(imported,moveEntry),movedOut);
            store.RemoveTransferred(imported,new[]{moveEntry},path=>{throw new Exception("moved source must not be recycled");});
            check(File.ReadAllText(movedOut)=="program bytes"&&!imported.Items.Contains(moveEntry),"successful drag-move removes empty capsule storage only");
            var oldReference=store.AddReference(imported,directory);string folderOut=Path.Combine(root,"folder-out");Directory.CreateDirectory(folderOut);
            store.RemoveTransferred(imported,new[]{oldReference},path=>{throw new Exception("reference original must not be recycled");});
            check(Directory.Exists(directory)&&Directory.Exists(folderOut)&&!imported.Items.Contains(oldReference),"dragging old reference out never deletes source folder");
            string migrationFile=Path.Combine(root,"migrate.url");File.WriteAllText(migrationFile,"migration");
            var toMigrate=store.AddReference(imported,migrationFile);
            check(store.MigrateSmallReference(imported,toMigrate),"old small reference migrates to copy");
            var migrated=imported.Items.First(x=>x.TargetPath==migrationFile);File.Delete(migrationFile);
            check(migrated.Extension==".copy"&&File.ReadAllText(store.EntryPath(imported,migrated))=="migration","migrated old reference survives original deletion");
            string missingOld=Path.Combine(root,"old-missing.txt");File.WriteAllText(missingOld,"old");var missingRef=store.AddReference(imported,missingOld);File.Delete(missingOld);
            check(!store.MigrateSmallReference(imported,missingRef)&&missingRef.IsReference,"missing old source remains reference");
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
