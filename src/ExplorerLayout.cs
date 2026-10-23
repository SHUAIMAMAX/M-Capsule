using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PocketGrid
{
    internal static class ExplorerLayout
    {
        public static Rectangle LeftHalf(Rectangle work)
        {
            return new Rectangle(work.Left,work.Top,Math.Max(1,work.Width/2),work.Height);
        }
        internal static async Task<bool> Arrange(Rectangle work,Func<IntPtr> locate,Func<IntPtr,Rectangle,bool> place,Func<Task> pause,int attempts)
        {
            var target=LeftHalf(work);
            for(int i=0;i<attempts;i++)
            {
                await pause();
                IntPtr handle=locate();
                if(handle!=IntPtr.Zero)return place(handle,target);
            }
            return false;
        }
        static object Property(object target,string name)
        {
            return target.GetType().InvokeMember(name,BindingFlags.GetProperty,null,target,null);
        }
        static IntPtr FindFolderWindow(string folder)
        {
            object shell=null,windows=null;
            try
            {
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                windows=shell.GetType().InvokeMember("Windows",BindingFlags.InvokeMethod,null,shell,null);
                int count=Convert.ToInt32(Property(windows,"Count"));
                for(int i=0;i<count;i++)
                {
                    object item=null;
                    try
                    {
                        item=windows.GetType().InvokeMember("Item",BindingFlags.InvokeMethod,null,windows,new object[]{i});
                        if(item==null)continue;
                        string location=Convert.ToString(Property(item,"LocationURL"));
                        Uri uri;
                        if(!Uri.TryCreate(location,UriKind.Absolute,out uri)||!uri.IsFile)continue;
                        if(String.Equals(Path.GetFullPath(uri.LocalPath).TrimEnd('\\'),folder.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))
                        {
                            var handle=new IntPtr(Convert.ToInt64(Property(item,"HWND")));
                            if(Native.IsWindow(handle)&&Native.ClassName(handle)=="CabinetWClass")return handle;
                        }
                    }
                    catch(Exception){/* Other Shell windows may be unavailable or mid-navigation. */}
                    finally{if(item!=null&&Marshal.IsComObject(item))Marshal.ReleaseComObject(item);}
                }
                return IntPtr.Zero;
            }
            finally
            {
                if(windows!=null&&Marshal.IsComObject(windows))Marshal.ReleaseComObject(windows);
                if(shell!=null&&Marshal.IsComObject(shell))Marshal.ReleaseComObject(shell);
            }
        }
        public static async Task<bool> OpenOnLeft(string folder,Rectangle work)
        {
            if(RuntimeMode.Sandbox)return true;
            Process.Start(new ProcessStartInfo("explorer.exe") { Arguments="\""+folder+"\"",UseShellExecute=true });
            return await Arrange(work,()=>FindFolderWindow(folder),(handle,target)=>
            {
                Native.ShowWindow(handle,9);
                return Native.SetWindowPos(handle,IntPtr.Zero,target.Left,target.Top,target.Width,target.Height,0x0014);
            },()=>Task.Delay(100),50);
        }
    }
}
