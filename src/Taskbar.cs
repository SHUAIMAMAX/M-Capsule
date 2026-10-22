using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace PocketGrid
{
    [ComImport, Guid("5bcf9150-c28a-4ef2-913c-4c3ea2f5ead0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITaskbarAppearance
    {
        [PreserveSig] int SetTaskbarAppearance(IntPtr taskbar, int brush, uint color);
        [PreserveSig] int SetTaskbarBlur(IntPtr taskbar, uint color, float amount);
        [PreserveSig] int ReturnTaskbarToDefaultAppearance(IntPtr taskbar);
        [PreserveSig] int SetTaskbarBorderVisibility(IntPtr taskbar, [MarshalAs(UnmanagedType.Bool)] bool visible);
        [PreserveSig] int RestoreAllTaskbarsToDefault();
        [PreserveSig] int RestoreAllTaskbarsToDefaultWhenProcessDies(uint pid);
        [PreserveSig] int KillExplorerWhenPackageUninstalls([MarshalAs(UnmanagedType.LPWStr)] string packageName);
    }
    internal sealed class TaskbarController : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Inject(IntPtr window, ref Guid iid, out IntPtr result);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr LoadLibraryEx(string path, IntPtr reserved, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("ole32.dll")] static extern int CoInitializeEx(IntPtr reserved, uint flags);
        [DllImport("ole32.dll")] static extern void CoUninitialize();
        readonly BlockingCollection<Action> queue = new BlockingCollection<Action>();
        readonly Thread thread;
        readonly Action<string> report;
        ITaskbarAppearance service;
        IntPtr library;
        bool disposed;
        int generation;
        public TaskbarController(Action<string> report)
        {
            if(RuntimeMode.Sandbox)throw new InvalidOperationException("沙箱禁止加载资源管理器效果组件。");
            this.report = report;
            thread = new Thread(Worker) { IsBackground = true, Name = "Capsule taskbar effects" };
            thread.SetApartmentState(ApartmentState.MTA); thread.Start();
        }
        void Worker()
        {
            CoInitializeEx(IntPtr.Zero, 0);
            try { foreach (var action in queue.GetConsumingEnumerable()) { try { action(); } catch (Exception ex) { Record("FAILED " + ex); report("任务栏效果未能应用：" + ex.Message); } } }
            finally
            {
                if (service != null) { try { service.RestoreAllTaskbarsToDefault(); } catch { } Marshal.FinalReleaseComObject(service); }
                CoUninitialize();
            }
        }
        public void Apply(string mode, bool reset = false)
        {
            if (disposed) return;
            int request=Interlocked.Increment(ref generation);
            queue.Add(() =>
            {
                if(request!=Volatile.Read(ref generation))return;
                if (reset && service != null) { Marshal.FinalReleaseComObject(service); service = null; }
                if (mode == "normal") { if (service != null) Marshal.ThrowExceptionForHR(service.RestoreAllTaskbarsToDefault()); Record("system default restored"); return; }
                if(mode!="acrylic"&&mode!="clear")throw new ArgumentOutOfRangeException("mode");
                if (Process.GetProcessesByName("TranslucentTB").Length > 0) throw new InvalidOperationException("请先退出另一个正在运行的 TranslucentTB，避免两套设置互相覆盖。");
                var bars = Native.Taskbars();
                if (bars.Count == 0) { Record("no taskbar window; waiting for Explorer TaskbarCreated"); return; }
                if (service == null)
                {
                    if (!Environment.Is64BitProcess) throw new InvalidOperationException("此任务栏效果组件需要 x64 Windows。");
                    if (library == IntPtr.Zero)
                    {
                        // Like upstream, use a versioned cache: Explorer pins this DLL until sign-out.
                        string dir = Path.Combine(Settings.DataDirectory, "Runtime", "TranslucentTB-2026.2-c48f1a06");
                        Directory.CreateDirectory(dir);
                        string dll = Path.Combine(dir, "ExplorerTAP.dll");
                        string source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vendor", "ExplorerTAP.dll");
                        if (!File.Exists(dll)) File.Copy(source, dll);
                        using (var hash = System.Security.Cryptography.SHA256.Create())
                        using (var input = File.OpenRead(dll))
                        {
                            string digest = BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                            if (digest != "c48f1a06168acd7839269dde0d15ccf5d09c702512b94dd6ba9a05df1b153dc2") throw new InvalidDataException("任务栏组件校验不匹配，请重新解压完整便携包。");
                        }
                        library = LoadLibraryEx(dll, IntPtr.Zero, 0x100 | 0x1000);
                        if (library == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    }
                    var address = GetProcAddress(library, "InjectExplorerTAP");
                    if (address == IntPtr.Zero) throw new InvalidDataException("任务栏组件缺少入口。");
                    var inject = (Inject)Marshal.GetDelegateForFunctionPointer(address, typeof(Inject));
                    Guid iid = typeof(ITaskbarAppearance).GUID; IntPtr raw;
                    IntPtr main = Native.FindWindow("Shell_TrayWnd", null);
                    if(main==IntPtr.Zero)throw new InvalidOperationException("未找到主任务栏窗口，请重启 Windows 资源管理器后重试。");
                    Marshal.ThrowExceptionForHR(inject(main, ref iid, out raw));
                    if(raw==IntPtr.Zero)throw new InvalidOperationException("任务栏效果服务未返回接口。");
                    try { service = (ITaskbarAppearance)Marshal.GetObjectForIUnknown(raw); } finally { Marshal.Release(raw); }
                    try { Marshal.ThrowExceptionForHR(service.RestoreAllTaskbarsToDefaultWhenProcessDies((uint)Process.GetCurrentProcess().Id)); }
                    catch { Marshal.FinalReleaseComObject(service); service = null; throw; }
                }
                // ExplorerTAP may return S_OK before XAML has registered its background.
                // Reapply briefly while the taskbar finishes creating; a newer selection wins.
                for(int attempt=0;attempt<8&&request==Volatile.Read(ref generation)&&!disposed;attempt++)
                {
                    foreach (var bar in bars)
                    {
                        // Upstream TranslucentTB 2026.2 uses XAML blur for blur mode on Windows 11.
                        if(mode=="acrylic") Marshal.ThrowExceptionForHR(service.SetTaskbarBlur(bar, 0x99252020u, 3.0f));
                        else Marshal.ThrowExceptionForHR(service.SetTaskbarAppearance(bar, 1, 0x00000000u));
                        Marshal.ThrowExceptionForHR(service.SetTaskbarBorderVisibility(bar, false));
                    }
                    if(attempt<7)Thread.Sleep(400);
                }
                Record("sent "+mode+" to "+bars.Count+" taskbar window(s); ExplorerTAP does not confirm visual change");
            });
        }
        static void Record(string message)
        {
            try
            {
                string dir=Path.Combine(Settings.DataDirectory,"Logs");Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir,"taskbar.log"),DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine);
            }
            catch{} // Diagnostics must not hide the actual taskbar result.
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; queue.CompleteAdding();
            // The upstream process-death watcher restores effects even if a COM request is stuck.
            thread.Join(1200);
        }
    }
}
