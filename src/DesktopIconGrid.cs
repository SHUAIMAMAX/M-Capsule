using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace PocketGrid
{
    // Read-only supported Shell view methods. Never selects or moves desktop icons.
    internal static class DesktopIconGrid
    {
        static DesktopGrid snapshot;
        static int reading;
        static readonly Guid browserService=new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");
        public static DesktopGrid Snapshot { get { return Volatile.Read(ref snapshot); } }
        public static void Refresh(Action<bool,bool> onReady)
        {
            if(RuntimeMode.Sandbox||Interlocked.Exchange(ref reading,1)!=0)return;
            var dispatcher=Application.Current.Dispatcher;
            var worker=new Thread(()=>
            {
                try
                {
                    var value=Read();
                    if(value!=null)
                    {
                        var previous=Snapshot;
                        bool spacingChanged=previous==null||previous.StepX!=value.StepX||previous.StepY!=value.StepY||previous.IconSize!=value.IconSize||
                            Phase(previous.OriginX,previous.StepX)!=Phase(value.OriginX,value.StepX)||
                            Phase(previous.OriginY,previous.StepY)!=Phase(value.OriginY,value.StepY);
                        bool iconsChanged=previous==null||!SameIcons(previous.Icons,value.Icons);
                        Volatile.Write(ref snapshot,value);
                        if(spacingChanged||iconsChanged)dispatcher.BeginInvoke(new Action(()=>onReady(spacingChanged,iconsChanged)));
                    }
                }
                catch(Exception ex){StartupLog.Write("desktop icon grid unavailable: "+ex.Message);}
                finally{Interlocked.Exchange(ref reading,0);}
            }){IsBackground=true,Name="Desktop icon grid"};
            worker.SetApartmentState(ApartmentState.STA);worker.Start();
        }
        static double Phase(double position,double step){return (position%step+step)%step;}
        static bool SameIcons(GridPoint[] a,GridPoint[] b)
        {
            if(a==null||b==null)return a==b;
            if(a.Length!=b.Length)return false;
            for(int i=0;i<a.Length;i++)if(a[i].X!=b[i].X||a[i].Y!=b[i].Y)return false;
            return true;
        }
        static DesktopGrid Read()
        {
            object shell=new CShellWindows(),dispatch=null,browser=null,view=null;
            try
            {
                object location=0,unused=new object();int hwnd;
                dispatch=((IShellWindows)shell).FindWindowSW(ref location,ref unused,8,out hwnd,1);
                if(dispatch==null)return null;
                Guid iid=typeof(IShellBrowser).GUID;
                Guid service=browserService;
                browser=((IServiceProvider)dispatch).QueryService(ref service,ref iid);
                view=((IShellBrowser)browser).QueryActiveShellView();
                var folder=(IFolderView)view;
                Native.Point spacing=new Native.Point();folder.GetSpacing(ref spacing);
                int iconPixels=ReadIconSize(view);
                if(spacing.X<40||spacing.Y<40||spacing.X>500||spacing.Y>500)return null;
                Native.Rect bounds;
                IntPtr handle;((IShellView)view).GetWindow(out handle);
                if(!Native.GetWindowRect(handle,out bounds))return null;
                int count;folder.ItemCount(2,out count);
                if(count<0||count>4096)return null;
                var points=new List<Native.Point>();
                for(int i=0;i<count;i++)
                {
                    IntPtr pidl=IntPtr.Zero;
                    try
                    {
                        folder.Item(i,out pidl);
                        Native.Point p;folder.GetItemPosition(pidl,out p);
                        points.Add(new Native.Point{X=bounds.Left+p.X,Y=bounds.Top+p.Y});
                    }
                    finally{if(pidl!=IntPtr.Zero)Marshal.FreeCoTaskMem(pidl);}
                }
                double originX=points.Count>0?points[0].X:bounds.Left+16;
                double originY=points.Count>0?points[0].Y:bounds.Top+16;
                return new DesktopGrid{StepX=spacing.X,StepY=spacing.Y,OriginX=originX,OriginY=originY,
                    IconSize=iconPixels>=16&&iconPixels<=256?iconPixels:64,
                    Icons=points.ConvertAll(p=>new GridPoint(p.X,p.Y)).ToArray()};
            }
            finally
            {
                if(view!=null)Marshal.ReleaseComObject(view);
                if(browser!=null)Marshal.ReleaseComObject(browser);
                if(dispatch!=null)Marshal.ReleaseComObject(dispatch);
                Marshal.ReleaseComObject(shell);
            }
        }
        static int ReadIconSize(object view)
        {
            // IFolderView2 extends IFolderView. This read-only method reports the
            // current image size in physical pixels, including Ctrl+wheel changes.
            IntPtr unknown=IntPtr.Zero,interfacePointer=IntPtr.Zero;
            try
            {
                unknown=Marshal.GetIUnknownForObject(view);
                Guid iid=new Guid("1AF3A467-214F-4298-908E-06B03E0B39F9");
                if(Marshal.QueryInterface(unknown,ref iid,out interfacePointer)!=0)return 64;
                IntPtr vtable=Marshal.ReadIntPtr(interfacePointer);
                IntPtr method=Marshal.ReadIntPtr(vtable,36*IntPtr.Size);
                var get=(GetViewModeAndIconSize)Marshal.GetDelegateForFunctionPointer(method,typeof(GetViewModeAndIconSize));
                int mode,size;
                return get(interfacePointer,out mode,out size)==0&&size>=16&&size<=256?size:64;
            }
            catch(Exception ex){StartupLog.Write("desktop icon size unavailable: "+ex.Message);return 64;}
            finally
            {
                if(interfacePointer!=IntPtr.Zero)Marshal.Release(interfacePointer);
                if(unknown!=IntPtr.Zero)Marshal.Release(unknown);
            }
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int GetViewModeAndIconSize(IntPtr view,out int mode,out int size);
        public static DesktopGrid InWindowCoordinates(Visual visual)
        {
            var grid=Snapshot;
            var source=PresentationSource.FromVisual(visual);
            if(grid==null||source==null||source.CompositionTarget==null)return null;
            var matrix=source.CompositionTarget.TransformFromDevice;
            var origin=matrix.Transform(new Point(grid.OriginX,grid.OriginY));
            var step=matrix.Transform(new Point(grid.StepX,grid.StepY))-matrix.Transform(new Point(0,0));
            var iconSize=matrix.Transform(new Point(grid.IconSize,0))-matrix.Transform(new Point(0,0));
            var icons=new GridPoint[grid.Icons.Length];
            for(int i=0;i<icons.Length;i++){var p=matrix.Transform(new Point(grid.Icons[i].X,grid.Icons[i].Y));icons[i]=new GridPoint(p.X,p.Y);}
            return new DesktopGrid{StepX=step.X,StepY=step.Y,OriginX=origin.X,OriginY=origin.Y,IconSize=Math.Abs(iconSize.X),Icons=icons};
        }
        [ComImport,Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"),ClassInterface(ClassInterfaceType.None)]sealed class CShellWindows{}
        [ComImport,Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"),InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]interface IShellWindows
        {
            [return:MarshalAs(UnmanagedType.IDispatch)]object FindWindowSW([MarshalAs(UnmanagedType.Struct)]ref object loc,[MarshalAs(UnmanagedType.Struct)]ref object root,int shellClass,out int hwnd,int options);
        }
        [ComImport,Guid("6d5140c1-7436-11ce-8034-00aa006009fa"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IServiceProvider
        {
            [return:MarshalAs(UnmanagedType.Interface)]object QueryService(ref Guid service,ref Guid iid);
        }
        [ComImport,Guid("000214E2-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IShellBrowser
        {
            void Gap01();void Gap02();void Gap03();void Gap04();void Gap05();void Gap06();void Gap07();void Gap08();void Gap09();void Gap10();void Gap11();void Gap12();
            [return:MarshalAs(UnmanagedType.Interface)]object QueryActiveShellView();
        }
        [ComImport,Guid("000214E3-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IShellView
        {
            void GetWindow(out IntPtr window);
        }
        [ComImport,Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IFolderView
        {
            void Gap01();void Gap02();void Gap03();
            void Item(int index,out IntPtr pidl);
            void ItemCount(uint flags,out int count);
            void Gap06();void Gap07();void Gap08();
            void GetItemPosition(IntPtr pidl,out Native.Point point);
            void GetSpacing(ref Native.Point point);
        }
    }
}
