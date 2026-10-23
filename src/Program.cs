using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace PocketGrid
{
    internal static class Program
    {
        static readonly System.Diagnostics.Stopwatch startupWatch=System.Diagnostics.Stopwatch.StartNew();
        static bool firstFrame;
        internal static void LogFirstFrame()
        {
            if(firstFrame)return;firstFrame=true;
            StartupLog.Write("first capsule rendered elapsed_ms="+startupWatch.ElapsedMilliseconds+" (managed entry to WPF ContentRendered; excludes Windows launch scheduling)");
        }
        [STAThread]
        internal static void Main(string[] args)
        {
            startupWatch.Restart();
            RuntimeMode.Initialize(args);
            StartupLog.Write("entry background="+args.Contains("--background")+" executable="+Settings.Executable+" data="+Settings.DataDirectory);
            if(args.Any(a=>a=="--cleanup"||a=="--finish-uninstall"||a=="--uninstall")) { MessageBox.Show("便携版无需安装器。请在胶囊管理窗口底部关闭开机自启，然后从托盘退出；按需备份 Data 后删除便携文件夹。若已删除程序，可从 shell:startup 手动移除本程序的启动快捷方式。",Ui.Product); return; }
            string[] launchArgs=args.Where(a=>a!="--sandbox").ToArray();
            string id=launchArgs.Length==2&&launchArgs[0]=="--capsule"&&Logic.IsId(launchArgs[1])?launchArgs[1]:null;
            Native.SetCurrentProcessExplicitAppUserModelID(id==null?"M.Capsule":"M.Capsule."+id);
            bool first;
            using(var mutex=new Mutex(true,"Local\\"+RuntimeMode.Identity+".Instance",out first))
            {
                if(!first)
                {
                    StartupLog.Write("another instance is running; this launch will exit");
                    IntPtr target=IntPtr.Zero;
                    for(int n=0;n<30&&target==IntPtr.Zero;n++){target=Native.FindWindow(null,RuntimeMode.ControllerTitle);if(target==IntPtr.Zero)Thread.Sleep(100);}
                    if(target!=IntPtr.Zero)
                    {
                        uint pid;Native.GetWindowThreadProcessId(target,out pid);Native.AllowSetForegroundWindow(pid);
                        if(id!=null)
                        {
                            var ptr=Marshal.StringToCoTaskMemUni(id);
                            try{var data=new Native.CopyData{Tag=new IntPtr(0x4D534243),Size=66,Data=ptr};IntPtr result;Native.SendMessageTimeout(target,Native.WM_COPYDATA,IntPtr.Zero,ref data,2,2000,out result);}
                            finally{Marshal.FreeCoTaskMem(ptr);}
                        }
                        else if(!args.Contains("--background"))Native.PostMessage(target,Native.Wake,IntPtr.Zero,IntPtr.Zero);
                    }
                    return;
                }
                Manager manager=null;
                var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
                app.DispatcherUnhandledException+=(s,e)=>
                {
                    StartupLog.Write("dispatcher failure "+e.Exception);
                    e.Handled=true;
                    if(manager!=null)manager.ClosePopup();
                    MessageBox.Show("运行遇到错误：\n"+e.Exception.Message+"\n\n可退出后重新启动，胶囊副本保存在本机。",Ui.Product);
                    if(manager!=null)manager.Dispose();app.Shutdown(1);
                };
                try
                {
                    manager=new Manager();app.Exit+=(s,e)=>manager.Dispose();
                    StartupLog.Write("manager ready capsules="+manager.Windows.Count);
                    if(id!=null)app.Dispatcher.BeginInvoke(new Action(()=>manager.OpenById(id)));
                    if(args.Contains("--welcome"))manager.Notify("已在桌面放置胶囊。悬停展开，右键可更名；把文件拖进来即可。托盘右键打开胶囊管理，点击绿色 + 可新建胶囊。");
                    app.Run();
                }
                catch(Exception ex){StartupLog.Write("startup failure "+ex);MessageBox.Show("无法启动：\n"+ex.Message+"\n原配置未被重置。",Ui.Product);if(manager!=null)manager.Dispose();}
                finally{mutex.ReleaseMutex();}
            }
        }
    }
}
