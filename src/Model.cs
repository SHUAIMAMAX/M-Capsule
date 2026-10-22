using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace PocketGrid
{
    public sealed class Shortcut
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Extension { get; set; } = ".lnk";
        public string Name { get; set; } = "";
        public string TargetPath { get; set; } = "";
        public bool IsReference { get { return Extension == ".ref"; } }
    }
    public sealed class Capsule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "小工具";
        public int ColorIndex { get; set; }
        public int IconOpacityPercent { get; set; } = 100;
        public bool HasCustomIcon { get; set; }
        public double X { get; set; } = 32;
        public double Y { get; set; } = 140;
        public List<Shortcut> Items { get; set; } = new List<Shortcut>();
    }
    public sealed class Preferences
    {
        public int Version { get; set; } = 3;
        public long Revision { get; set; }
        public int NextColor { get; set; }
        public string TaskbarMode { get; set; } = "normal";
        public bool StartWithWindows { get; set; } = true;
        public List<Capsule> Capsules { get; set; } = new List<Capsule>();
    }
    public sealed class GridSize
    {
        public int Columns, Rows;
        public double Width, Height;
        public int PageSize;
    }
    public static class Logic
    {
        public const int MaxCapsules = 32, MaxItems = 512;
        public static bool IsId(string value) { Guid id; return value != null && value.Length == 32 && Guid.TryParseExact(value, "N", out id); }
        public static bool IsShortcut(string path)
        {
            string ext = Path.GetExtension(path);
            return String.Equals(ext, ".lnk", StringComparison.OrdinalIgnoreCase) || String.Equals(ext, ".url", StringComparison.OrdinalIgnoreCase);
        }
        public static string CleanName(string value)
        {
            var s = new string((value ?? "").Where(c => !Char.IsControl(c)).ToArray()).Trim();
            return s.Length == 0 ? "小工具" : s.Substring(0, Math.Min(40, s.Length));
        }
        public static double Clamp(double x, double a, double b)
        {
            return Double.IsNaN(x) || Double.IsInfinity(x) ? a : Math.Max(a, Math.Min(x, Math.Max(a, b)));
        }
        public static GridSize Layout(int count, double availableWidth, double availableHeight)
        {
            count = Math.Max(4, Math.Min(MaxItems, count));
            double maxW = Math.Max(260, availableWidth - 24), maxH = Math.Max(272, availableHeight - 24);
            int maxColumns = Math.Max(2, (int)((maxW - 48) / 84));
            int maxRows = Math.Max(2, (int)((maxH - 96) / 88));
            int columns = Math.Min(maxColumns, (int)Math.Ceiling(Math.Sqrt(count)));
            int rows = Math.Max(2, (count + columns - 1) / columns);
            // Use extra columns before paging when the screen cannot fit every row.
            while (rows > maxRows && columns < maxColumns && columns < count)
            {
                columns++; rows = Math.Max(2, (count + columns - 1) / columns);
            }
            rows = Math.Min(rows, maxRows);
            double naturalW = columns * 84 + 48, naturalH = rows * 88 + 96;
            double width = Math.Min(maxW, Math.Max(naturalW, Math.Min(naturalH, naturalW + 72)));
            double height = Math.Min(maxH, naturalH);
            return new GridSize { Columns = columns, Rows = rows, Width = width, Height = height, PageSize = columns * rows };
        }
        public static string Serialize(Preferences p)
        {
            using (var writer = new StringWriter()) { new XmlSerializer(typeof(Preferences)).Serialize(writer, p); return writer.ToString(); }
        }
        public static Preferences Deserialize(string xml)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 };
            using (var input = new StringReader(xml))
            using (var reader = XmlReader.Create(input, settings))
            {
                var p = (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(reader);
                if ((p.Version != 2 && p.Version != 3) || p.Capsules == null || p.Capsules.Count > MaxCapsules) throw new InvalidDataException("配置版本或胶囊数量无效。");
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in p.Capsules)
                {
                    if (c == null || !IsId(c.Id) || !ids.Add(c.Id) || c.Items == null || c.Items.Count > MaxItems) throw new InvalidDataException("胶囊配置无效。");
                    c.Name = CleanName(c.Name); c.ColorIndex = Math.Max(0, c.ColorIndex);
                    c.IconOpacityPercent = Math.Max(0, Math.Min(100, c.IconOpacityPercent));
                    var entryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in c.Items)
                    {
                        if (entry == null || !IsId(entry.Id) || !entryIds.Add(entry.Id) || (entry.Extension != ".lnk" && entry.Extension != ".url" && entry.Extension != ".ref")) throw new InvalidDataException("条目配置无效。");
                        if(entry.IsReference) ShortcutStore.ValidateTarget(entry.TargetPath);
                    }
                }
                p.Version=3;
                if (!new[] { "normal", "acrylic", "clear" }.Contains(p.TaskbarMode)) p.TaskbarMode = "normal";
                p.NextColor = Math.Max(0, p.NextColor);
                if (p.Revision < 0 || p.Revision == long.MaxValue) throw new InvalidDataException("配置序号无效。");
                return p;
            }
        }
    }
    public sealed class ShortcutStore
    {
        public readonly string Root;
        public ShortcutStore(string root) { Root = Path.GetFullPath(root); }
        public string CapsuleDirectory(Capsule c)
        {
            if (!Logic.IsId(c.Id)) throw new InvalidDataException("胶囊 ID 无效。");
            return Path.Combine(Root, c.Id);
        }
        public string EntryPath(Capsule c, Shortcut e)
        {
            CapsuleDirectory(c);
            if(e.IsReference) { if(!Logic.IsId(e.Id))throw new InvalidDataException("条目 ID 无效。");return ValidateTarget(e.TargetPath); }
            if (!Logic.IsId(e.Id) || (e.Extension != ".lnk" && e.Extension != ".url")) throw new InvalidDataException("快捷方式 ID 无效。");
            return Path.Combine(CapsuleDirectory(c), e.Id + e.Extension);
        }
        static void CheckDirectory(string dir)
        {
            if (Directory.Exists(dir) && (File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) throw new IOException("快捷方式目录不能是符号链接或目录联接。");
        }
        public static string ValidateTarget(string path)
        {
            // Accept absolute filesystem paths only; no URI, shell command, device or ADS paths.
            if(String.IsNullOrWhiteSpace(path)||path.IndexOfAny(new[]{'"','\r','\n','\0'})>=0 ||
               path.StartsWith(@"\\?\")||path.StartsWith(@"\\.\")||
               !(path.StartsWith(@"\\")||(path.Length>=3&&Char.IsLetter(path[0])&&path[1]==':'&&(path[2]=='\\'||path[2]=='/'))) ||
               path.IndexOf(':',path.StartsWith(@"\\")?0:2)>=0)
                throw new InvalidDataException("请选择有效的文件或文件夹绝对路径。");
            try { return Path.GetFullPath(path); }
            catch(ArgumentException ex){throw new InvalidDataException("文件路径无效。",ex);}
            catch(NotSupportedException ex){throw new InvalidDataException("文件路径无效。",ex);}
            catch(PathTooLongException ex){throw new InvalidDataException("文件路径过长。",ex);}
        }
        public Shortcut AddReference(Capsule c,string source)
        {
            CapsuleDirectory(c);
            string target=ValidateTarget(source);
            if(!File.Exists(target)&&!Directory.Exists(target))throw new IOException("文件或文件夹不存在或暂时无法访问。");
            var existing=c.Items.FirstOrDefault(e=>e.IsReference&&String.Equals(e.TargetPath,target,StringComparison.OrdinalIgnoreCase));
            if(existing!=null)return existing;
            if(c.Items.Count>=Logic.MaxItems)throw new IOException("每个胶囊最多放 512 个条目。");
            string name=Path.GetFileName(target.TrimEnd('\\','/'));
            if(String.IsNullOrEmpty(name))name=target;
            if(Logic.IsShortcut(target))name=Path.GetFileNameWithoutExtension(target);
            var item=new Shortcut{Extension=".ref",TargetPath=target,Name=name};
            c.Items.Add(item);
            return item;
        }
        public Shortcut CopyIn(Capsule c, string source)
        {
            if (c.Items.Count >= Logic.MaxItems) throw new IOException("每个胶囊最多放 512 个快捷方式。");
            if (!Logic.IsShortcut(source) || !File.Exists(source)) throw new IOException("请添加 .lnk 或 .url 快捷方式。");
            CheckDirectory(Root); string dir = CapsuleDirectory(c); CheckDirectory(dir);
            Directory.CreateDirectory(dir);
            var item = new Shortcut { Extension = Path.GetExtension(source).ToLowerInvariant(), Name = Path.GetFileNameWithoutExtension(source) };
            string destination = EntryPath(c, item);
            File.Copy(source, destination, false);
            File.SetAttributes(destination, File.GetAttributes(destination) & ~FileAttributes.ReadOnly);
            c.Items.Add(item);
            return item;
        }
        public void Remove(Capsule c, IEnumerable<Shortcut> entries, Action<string> recycle)
        {
            CheckDirectory(Root); CheckDirectory(CapsuleDirectory(c));
            foreach (var entry in entries.ToArray())
            {
                if (!c.Items.Contains(entry)) continue;
                string file = EntryPath(c, entry);
                if (!entry.IsReference && File.Exists(file)) recycle(file);
                c.Items.Remove(entry);
            }
        }
        public void RemoveCapsule(Capsule c, Action<string> recycle)
        {
            CheckDirectory(Root); string dir = CapsuleDirectory(c); CheckDirectory(dir);
            if (Directory.Exists(dir)) recycle(dir);
        }
    }
}
