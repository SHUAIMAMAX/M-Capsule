using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PocketGrid
{
    internal static class Native
    {
        public const int Wake = 0x8001, WM_COPYDATA = 0x004A;
        public static readonly uint TaskbarCreated = RegisterWindowMessage("TaskbarCreated");
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct CopyData { public IntPtr Tag; public int Size; public IntPtr Data; }
        public delegate void WinEvent(IntPtr hook, uint evt, IntPtr hwnd, int obj, int child, uint thread, uint time);
        public delegate IntPtr MouseHook(int code, IntPtr wp, IntPtr lp);
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr parameter);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr GetTopWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder name, int size);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr hwnd, IntPtr update, IntPtr region, uint flags);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(uint pid);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wp, ref CopyData data, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr p);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEvent proc, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SetWindowsHookEx(int id, MouseHook proc, IntPtr module, uint tid);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wp, IntPtr lp);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetLong64(IntPtr h, int n);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetLong64(IntPtr h, int n, IntPtr v);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetLong32(IntPtr h, int n);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] static extern int SetLong32(IntPtr h, int n, int v);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        [DllImport("shell32.dll")] public static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string id);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path, uint attr, out FileInfo info, uint size, uint flags);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct FileInfo
        {
            public IntPtr Icon; public int Index; public uint Attributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string Type;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHFileOperation(ref FileOperation operation);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct FileOperation
        {
            public IntPtr Window; public uint Function;
            [MarshalAs(UnmanagedType.LPWStr)] public string From;
            [MarshalAs(UnmanagedType.LPWStr)] public string To;
            public ushort Flags;
            [MarshalAs(UnmanagedType.Bool)] public bool Aborted;
            public IntPtr Mappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string Title;
        }
        public static void Recycle(string path)
        {
            var op = new FileOperation { Function = 3, From = path + "\0\0", Flags = 0x40 | 0x10 | 0x4 | 0x400, Title = "M-Capsule" };
            int result = SHFileOperation(ref op);
            if (result != 0 || op.Aborted) throw new System.IO.IOException("未能将胶囊副本移入回收站。原快捷方式未受影响。");
        }
        public static string ClassName(IntPtr hwnd)
        {
            var name = new StringBuilder(256); GetClassName(hwnd, name, name.Capacity); return name.ToString();
        }
        public static bool DesktopForeground()
        {
            string name = ClassName(GetForegroundWindow()); return name == "Progman" || name == "WorkerW";
        }
        public static bool PointerOver(IntPtr hwnd)
        {
            var cursor=System.Windows.Forms.Cursor.Position;
            var hit=WindowFromPoint(new Point{X=cursor.X,Y=cursor.Y});
            return hwnd!=IntPtr.Zero && hit!=IntPtr.Zero && GetAncestor(hit,2)==hwnd;
        }
        public static void StyleAnchor(IntPtr hwnd)
        {
            long old = IntPtr.Size == 8 ? GetLong64(hwnd, -20).ToInt64() : GetLong32(hwnd, -20);
            long value = old | 0x08000000 | 0x80;
            if (IntPtr.Size == 8) SetLong64(hwnd, -20, new IntPtr(value)); else SetLong32(hwnd, -20, (int)value);
        }
        public static bool DesktopLayer(IntPtr hwnd, bool reveal)
        {
            if(RuntimeMode.Sandbox)return false;
            if(IsIconic(hwnd))ShowWindow(hwnd,4);
            IntPtr previous = IntPtr.Zero;
            for (var h = GetTopWindow(IntPtr.Zero); h != IntPtr.Zero; h = GetWindow(h, 2))
            {
                if (h == hwnd || !IsWindowVisible(h)) continue;
                var name = ClassName(h);
                if (name == "Progman" || name == "WorkerW")
                {
                    // Keep the capsule directly above the desktop, below normal app windows.
                    // It is then already visible when those windows minimize; no delayed raise to HWND_TOP.
                    bool placed=SetWindowPos(hwnd, previous, 0, 0, 0, 0, 0x53);
                    if(reveal)RedrawWindow(hwnd,IntPtr.Zero,IntPtr.Zero,0x0181);
                    return placed;
                }
                previous = h;
            }
            return false;
        }
        public static ImageSource IconFor(string path)
        {
            if(RuntimeMode.Sandbox)return null;
            FileInfo info;
            if (SHGetFileInfo(path, 0, out info, (uint)Marshal.SizeOf(typeof(FileInfo)), 0x100) == IntPtr.Zero || info.Icon == IntPtr.Zero) return null;
            try
            {
                var image = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); image.Freeze(); return image;
            }
            finally { DestroyIcon(info.Icon); }
        }
        public static bool TaskbarAt(int x,int y)
        {
            if(RuntimeMode.Sandbox)return false;
            foreach(var hwnd in Taskbars())
            {
                Rect rect;
                if(IsWindowVisible(hwnd)&&GetWindowRect(hwnd,out rect)&&x>=rect.Left&&x<rect.Right&&y>=rect.Top&&y<rect.Bottom)return true;
            }
            return false;
        }
        public static List<IntPtr> Taskbars()
        {
            var result = new List<IntPtr>();
            EnumWindows((h,p) => { var c = ClassName(h); if (c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd") result.Add(h); return true; }, IntPtr.Zero);
            return result;
        }
    }
}
