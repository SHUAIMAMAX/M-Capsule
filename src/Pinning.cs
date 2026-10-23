using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace PocketGrid
{
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")] class ShellLinkObject { }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    interface IShellLink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int size, IntPtr data, uint flags);
        void GetIDList(out IntPtr list); void SetIDList(IntPtr list);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int size);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int size);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int size);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey); void SetHotkey(short hotkey);
        void GetShowCmd(out int command); void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int size, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
    [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit)] struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public IntPtr Pointer;
        [FieldOffset(16)] public IntPtr Padding;
    }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }
    internal static class Pinning
    {
        static readonly Guid AppModel = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
        static void Set(IPropertyStore store, uint id, string text)
        {
            var key = new PropertyKey { Format = AppModel, Id = id };
            var value = new PropVariant { Type = 31, Pointer = Marshal.StringToCoTaskMemUni(text) };
            try { Marshal.ThrowExceptionForHR(store.SetValue(ref key, ref value)); } finally { Marshal.FreeCoTaskMem(value.Pointer); }
        }
        public static string AppId(Capsule c) { return "M.Capsule." + c.Id; }
        public static string Make(Capsule c)
        {
            string directory = Path.Combine(Settings.DataDirectory, "Pins", c.Id);
            Directory.CreateDirectory(directory);
            string name = new string(c.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray()).TrimEnd('.', ' ');
            if (name.Length == 0) name = "胶囊";
            // Prefix avoids DOS device names; the description and capsule keep the original name.
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) name = "胶囊-" + name;
            string linkFile = Path.Combine(directory, name + ".lnk"), iconFile = Path.Combine(directory, "Capsule.ico");
            if (!File.Exists(iconFile)) Icons.SaveIco(iconFile, c.ColorIndex);
            Create(linkFile, Settings.Executable, (RuntimeMode.Sandbox?"--sandbox ":"")+"--capsule " + c.Id, c.Name, iconFile, AppId(c));
            foreach (string old in Directory.GetFiles(directory, "*.lnk")) if (!String.Equals(old, linkFile, StringComparison.OrdinalIgnoreCase)) File.Delete(old);
            return linkFile;
        }
        public static void Create(string path, string target, string arguments, string title, string icon, string appId)
        {
            object instance = new ShellLinkObject();
            try
            {
                var link = (IShellLink)instance;
                link.SetPath(target); link.SetArguments(arguments); link.SetWorkingDirectory(Path.GetDirectoryName(target));
                link.SetDescription(title); link.SetIconLocation(icon, 0); link.SetShowCmd(1);
                var store = (IPropertyStore)instance;
                Set(store, 5, appId); Set(store, 2, "\"" + target + "\" " + arguments);
                Set(store, 3, icon); Marshal.ThrowExceptionForHR(store.Commit());
                ((IPersistFile)instance).Save(path, true);
            }
            finally { Marshal.FinalReleaseComObject(instance); }
        }
    }
}
