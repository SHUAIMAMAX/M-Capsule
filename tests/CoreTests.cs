using System;
using System.IO;
using System.Linq;
using PocketGrid;

using System.Runtime.InteropServices;
public static class CoreTests {
[DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateSymbolicLink(string link,string target,int flags);
public static void Main(){
int count=0;
void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
bool Reject(Action action){try{action();return false;}catch{return true;}}
var root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Capsule-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try
{
    string original=Path.Combine(root,"原始文件 & 中文.lnk"),other=Path.Combine(root,"第二个.url");
    File.WriteAllBytes(original,new byte[]{1,2,3,4,5});File.WriteAllText(other,"[InternetShortcut]\nURL=https://example.com/");
    var files=new ShortcutStore(Path.Combine(root,"managed"));var c=new Capsule{Name="模型 & 工具",ColorIndex=0};
    var a=files.CopyIn(c,original);var b=files.CopyIn(c,other);
    string copy=files.EntryPath(c,a);
    Check(copy!=original&&File.Exists(copy),"Shortcut is a physically independent copy");
    Check(File.ReadAllBytes(copy).SequenceEqual(File.ReadAllBytes(original)),"Copy preserves shortcut bytes");
    Check(c.Items.Count==2,"Batch may append multiple shortcuts");
    File.Delete(original);
    Check(File.Exists(copy)&&File.ReadAllBytes(copy).Length==5,"Deleting original shortcut leaves capsule copy intact");
    File.WriteAllText(original,"new original data");
    files.Remove(c,new[]{a},path=>File.Delete(path));
    Check(File.ReadAllText(original)=="new original data","Delete entry never touches original shortcut");
    Check(!File.Exists(copy)&&c.Items.Count==1,"Delete removes only managed copy and entry");
    var p=new Preferences{NextColor=2,TaskbarMode="acrylic"};p.Capsules.Add(c);
    var restored=Logic.Deserialize(Logic.Serialize(p));
    Check(restored.Capsules[0].Name==c.Name&&restored.Capsules[0].Items[0].Id==b.Id,"Names and managed-copy identity survive serialization");
    Check(restored.TaskbarMode=="acrylic"&&restored.NextColor==2,"Taskbar mode and palette sequence persist");
    Check(restored.StartWithWindows,"autostart preference survives XML round trip");
    var legacy=Logic.Deserialize(Logic.Serialize(p).Replace("<StartWithWindows>true</StartWithWindows>",""));
    Check(legacy.StartWithWindows,"older portable configuration defaults to autostart");
    p.StartWithWindows=false;
    Check(!Logic.Deserialize(Logic.Serialize(p)).StartWithWindows,"disabled autostart remains disabled after reload");
    Check(File.Exists(files.EntryPath(restored.Capsules[0],restored.Capsules[0].Items[0])),"Reload finds existing copy without source path");
    var second=new Capsule{Name="第二个",ColorIndex=1};var secondItem=files.CopyIn(second,other);
    files.RemoveCapsule(c,path=>Directory.Delete(path,true));
    Check(File.Exists(other),"Delete capsule leaves original shortcuts");
    Check(File.Exists(files.EntryPath(second,secondItem)),"Delete capsule leaves other capsules intact");
    Check(!Directory.Exists(files.CapsuleDirectory(c)),"Delete capsule affects only its owned directory");
    Check(Reject(()=>files.CopyIn(second,Path.Combine(root,"something.exe"))),"Only .lnk and .url imports accepted");
    Check(Reject(()=>files.CapsuleDirectory(new Capsule{Id="../../outside"})),"Reject capsule path traversal");
    Check(Reject(()=>files.EntryPath(second,new Shortcut{Id="../outside"})),"Reject entry path traversal");
    Check(Reject(()=>files.EntryPath(second,new Shortcut{Extension=".exe"})),"Reject extension substitution");
    Check(Reject(()=>Logic.Deserialize("<broken>")),"Reject corrupt config rather than resetting");
    Check(Reject(()=>Logic.Deserialize("<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///etc/passwd'>]><Preferences>&a;</Preferences>")),"External entities are prohibited");
    var duplicate=new Preferences();duplicate.Capsules.Add(second);duplicate.Capsules.Add(second);
    Check(Reject(()=>Logic.Deserialize(Logic.Serialize(duplicate))),"Reject duplicate capsule IDs");
    Check(Logic.CleanName("  设计\n工具  ")=="设计工具","Clean names without losing Unicode");
    Check(Logic.CleanName(new string('字',60)).Length==40,"Name length bound");
    Check(Logic.Clamp(double.NaN,10,100)==10&&Logic.Clamp(double.PositiveInfinity,10,100)==10,"Recover invalid positions");
    Check(Logic.Clamp(-2000,-1920,-90)==-1920,"Negative multi-monitor coordinates");
    foreach(int n in new[]{0,1,2,3,4,8,12,16,32,100,512})
    {
        var layout=Logic.Layout(n,1920,1040);
        Check(layout.PageSize==layout.Columns*layout.Rows&&layout.PageSize>=4,"Visible page capacity matches grid: "+n);
        Check(layout.Width<=1896&&layout.Height<=1016,"Panel stays inside work area: "+n);
    }
    var small=Logic.Layout(512,800,600);Check(small.PageSize<512&&small.PageSize>=4,"Overflow uses pages within the screen");
    var five=Logic.Layout(5,1920,1040);Check(five.PageSize>=5&&five.Height>=five.Rows*88+96,"Five icons fit without a scrollbar");
    var one=Logic.Layout(1,1920,1040);var four=Logic.Layout(4,1920,1040);
    Check(one.Width==four.Width&&one.Height==four.Height,"One to four entries share a minimum 2x2 panel");
    foreach(int n in new[]{0,1,2,3,4}){var grid=Logic.Layout(n,1920,1040);Check(grid.Columns==2&&grid.Rows==2,"Minimum 2x2 including empty capsule: "+n);}
    Check(Logic.Layout(0,220,220).Columns==2&&Logic.Layout(0,220,220).Rows==2,"Keep 2x2 minimum even in a very small work area");
    Check(Logic.Layout(4,800,230).Height>=260,"Minimum panel height is not reduced to one row");
    Check(Logic.Layout(9,1920,1040).Width>four.Width,"Panel grows beyond four entries");
    Check(Math.Abs(four.Width-four.Height)<25,"Small grids are approximately square");
    var failed=new Capsule();files.CopyIn(failed,other);
    Check(Reject(()=>files.Remove(failed,failed.Items.ToArray(),path=>throw new IOException("recycle failure")))&&failed.Items.Count==1,"Failed deletion keeps entry");
    var healthy=new Preferences{Revision=10,NextColor=2};healthy.Capsules.Add(second);
    string good=Logic.Serialize(healthy);
    var recovery=Recovery.Select(new[]{new ConfigCandidate("registry","<broken>"),new ConfigCandidate("registry-backup",good)},files.Root);
    Check(recovery.Recovered&&recovery.Value.Capsules[0].Name==second.Name,"Corrupt registry recovers named capsules from valid backup");
    Check(File.Exists(files.EntryPath(recovery.Value.Capsules[0],recovery.Value.Capsules[0].Items[0])),"Recovered configuration points at surviving copies");
    var newer=Logic.Deserialize(good);newer.Revision=11;newer.Capsules[0].Name="最新名称";
    recovery=Recovery.Select(new[]{new ConfigCandidate("registry",good),new ConfigCandidate("disk",Logic.Serialize(newer))},files.Root);
    Check(recovery.Value.Revision==11&&recovery.Value.Capsules[0].Name=="最新名称","Choose newest valid revision across registry and disk");
    recovery=Recovery.Select(new[]{new ConfigCandidate("registry",good),new ConfigCandidate("disk",good)},files.Root);
    Check(!recovery.Recovered&&recovery.Source=="registry","A healthy current configuration is retained");
    recovery=Recovery.Select(new[]{new ConfigCandidate("registry","broken"),new ConfigCandidate("registry-backup","broken too")},files.Root);
    Check(recovery.Source=="files"&&recovery.Value.Capsules.Any(x=>x.Id==second.Id),"Both registry values corrupt: rebuild from managed folders");
    var reconstructed=recovery.Value.Capsules.First(x=>x.Id==second.Id);
    Check(reconstructed.Items.Any(x=>x.Id==secondItem.Id),"Reconstruction retains shortcut identity");
    Check(File.Exists(other),"Recovery never changes original shortcuts");
    var emptyRoot=Path.Combine(root,"empty");Directory.CreateDirectory(emptyRoot);
    recovery=Recovery.Select(new[]{new ConfigCandidate("registry","broken")},emptyRoot);
    Check(recovery.Recovered&&recovery.Source=="new"&&recovery.Value.Capsules.Count==1,"Unrecoverable config still starts with an explicitly reported empty capsule");
    recovery=Recovery.Select(new ConfigCandidate[0],emptyRoot);
    Check(!recovery.Recovered&&recovery.Value.Capsules[0].Items.Count==0,"Fresh install starts normally with an empty capsule");
    var nullEntry=new Preferences();nullEntry.Capsules.Add(null);
    recovery=Recovery.Select(new[]{new ConfigCandidate("registry",Logic.Serialize(nullEntry)),new ConfigCandidate("disk",good)},emptyRoot);
    Check(recovery.Source=="disk","Null capsule in XML cannot bypass backup recovery");
    var snapshot=Path.Combine(root,"snapshots","configuration.xml");
    Recovery.WriteSnapshot(snapshot,good);Recovery.WriteSnapshot(snapshot,Logic.Serialize(newer));
    Check(Logic.Deserialize(File.ReadAllText(snapshot)).Revision==11&&Logic.Deserialize(File.ReadAllText(snapshot+".bak")).Revision==10,"Atomic replace keeps the previous valid snapshot");
    File.WriteAllText(snapshot,"broken primary");Recovery.WriteSnapshot(snapshot,Logic.Serialize(newer));
    Check(Logic.Deserialize(File.ReadAllText(snapshot+".bak")).Revision==10,"Damaged primary never overwrites the good backup");
    Check(Directory.GetFiles(Path.GetDirectoryName(snapshot),"*.damaged-*").Length==1,"Retain the damaged snapshot for recovery");
    Check(Reject(()=>Recovery.WriteSnapshot(snapshot,"bad new config"))&&Logic.Deserialize(File.ReadAllText(snapshot)).Revision==11,"Reject invalid replacement before touching saved configuration");
    File.WriteAllText(snapshot+".pending",Logic.Serialize(newer));
    recovery=Recovery.Select(new[]{new ConfigCandidate("registry",good),new ConfigCandidate("disk.pending",File.ReadAllText(snapshot+".pending"))},files.Root);
    Check(recovery.Source=="disk.pending","A complete interrupted write can recover the latest state");
    string oldData=Path.Combine(root,"old-data"),newData=Path.Combine(root,"chosen-install","Data");
    Directory.CreateDirectory(Path.Combine(oldData,"Capsules",second.Id));
    File.Copy(files.EntryPath(second,secondItem),Path.Combine(oldData,"Capsules",second.Id,secondItem.Id+secondItem.Extension));
    Directory.CreateDirectory(Path.Combine(oldData,"Config"));File.WriteAllText(Path.Combine(oldData,"Config","configuration.xml"),good);
    Directory.CreateDirectory(Path.Combine(oldData,"Cache"));File.WriteAllText(Path.Combine(oldData,"Cache","unused"),"old cache");
    InstallFiles.CopyData(oldData,newData);
    Check(File.Exists(Path.Combine(newData,"Capsules",second.Id,secondItem.Id+secondItem.Extension)),"Custom install migration copies shortcuts into installation Data");
    Check(File.Exists(Path.Combine(newData,"Config","configuration.xml")),"Migration includes configuration backups");
    Check(!Directory.Exists(Path.Combine(newData,"Cache")),"Old caches are not migrated into active data");
    InstallFiles.CopyData(oldData,newData);Check(true,"Identical migration can be retried safely");
    File.WriteAllText(Path.Combine(newData,"Config","configuration.xml"),"different");
    Check(Reject(()=>InstallFiles.CopyData(oldData,newData)),"Migration refuses to overwrite conflicting data");
    Check(File.ReadAllText(Path.Combine(oldData,"Config","configuration.xml"))==good,"Failed migration preserves the source");
    Check(Reject(()=>InstallFiles.CopyData(oldData,Path.Combine(oldData,"nested"))),"Prevent recursive migration into source subtree");
    Check(Reject(()=>InstallFiles.Normalize(Path.GetPathRoot(root))),"Refuse installation into drive/filesystem root");
    File.WriteAllText(Path.Combine(newData,"unrelated.txt"),"keep");
    var remaining=InstallFiles.DeleteOwnedData(newData);
    Check(remaining.Count==0&&!Directory.Exists(Path.Combine(newData,"Capsules")),"Uninstall deletes all owned capsule copies");
    Check(!Directory.Exists(Path.Combine(newData,"Config")),"Uninstall deletes configuration backup files");
    Check(File.Exists(other)&&File.Exists(Path.Combine(newData,"unrelated.txt")),"Uninstall preserves originals and unrelated files");
    string outside=Path.Combine(root,"outside"),junction=Path.Combine(root,"junction-data");Directory.CreateDirectory(outside);File.WriteAllText(Path.Combine(outside,"keep.txt"),"keep");
    if(!CreateSymbolicLink(junction,outside,3)) { Console.WriteLine("SKIP symlink test: Win32 "+Marshal.GetLastWin32Error()); } else { InstallFiles.DeleteOwnedData(junction);
    Check(File.Exists(Path.Combine(outside,"keep.txt")),"Cleanup unlinks a data-directory symlink without following it"); }
    Console.WriteLine("PASS: "+count+" behavior checks. Windows registry, COM, UI and taskbar require real Windows validation.");
}
finally{Directory.Delete(root,true);}

}
}
