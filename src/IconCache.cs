using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PocketGrid
{
    internal sealed class IconCache : IDisposable
    {
        sealed class Request
        {
            public string Key,Path;
            public readonly List<Action<ImageSource>> Callbacks=new List<Action<ImageSource>>();
        }
        [DllImport("ole32.dll")]static extern int CoInitializeEx(IntPtr reserved,uint flags);
        [DllImport("ole32.dll")]static extern void CoUninitialize();
        readonly object gate=new object();
        readonly Dictionary<string,ImageSource> cache=new Dictionary<string,ImageSource>();
        readonly Queue<string> order=new Queue<string>();
        readonly Dictionary<string,Request> pending=new Dictionary<string,Request>();
        readonly BlockingCollection<Request> requests=new BlockingCollection<Request>();
        readonly Dispatcher dispatcher;
        readonly string directory=System.IO.Path.Combine(Settings.DataDirectory,"Cache","Icons");
        bool disposed;
        bool workersStarted;
        public IconCache(Dispatcher dispatcher)
        {
            this.dispatcher=dispatcher;
        }
        public ImageSource Get(string key,string path,Action<ImageSource> callback)
        {
            lock(gate)
            {
                if(disposed)return null;
                if(!workersStarted)
                {
                    workersStarted=true;
                    // Start only when a visible page requests icons; no login-time prefetch.
                    for(int n=0;n<2;n++){var worker=new Thread(Worker){IsBackground=true,Name="Capsule icons"};worker.SetApartmentState(ApartmentState.STA);worker.Start();}
                }
                ImageSource image;if(cache.TryGetValue(key,out image))return image;
                Request request;
                if(!pending.TryGetValue(key,out request)){request=new Request{Key=key,Path=path};pending.Add(key,request);requests.Add(request);}
                if(callback!=null)request.Callbacks.Add(callback);
                return null;
            }
        }
        public void Warm(Capsule capsule,ShortcutStore store)
        {
            foreach(var item in capsule.Items)Get(capsule.Id+item.Id,store.EntryPath(capsule,item),null);
        }
        void Worker()
        {
            int initialized=CoInitializeEx(IntPtr.Zero,2);
            try
            {
                foreach(var request in requests.GetConsumingEnumerable())
                {
                    lock(gate){if(disposed)break;}
                    ImageSource image=null;
                    try
                    {
                        string file=System.IO.Path.Combine(directory,request.Key+".png");
                        if(File.Exists(file))
                        {
                            try
                            {
                                using(var stream=File.OpenRead(file))
                                {
                                    var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();image=bitmap;
                                }
                            }
                            catch{try{File.Delete(file);}catch{}}
                        }
                        if(image==null)
                        {
                            image=Native.IconFor(request.Path);
                            if(image is BitmapSource)
                            {
                                try
                                {
                                    Directory.CreateDirectory(directory);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create((BitmapSource)image));
                                    using(var output=new FileStream(file,FileMode.Create,FileAccess.Write,FileShare.None))encoder.Save(output);
                                }
                                catch{} // A cache write failure must not stop launch or rendering.
                            }
                        }
                    }
                    catch{} // Keep the placeholder when a shell extension cannot provide an icon.
                    Action<ImageSource>[] callbacks;
                    lock(gate)
                    {
                        if(disposed)break;
                        pending.Remove(request.Key);
                        cache[request.Key]=image;order.Enqueue(request.Key);
                        while(order.Count>512)cache.Remove(order.Dequeue());
                        callbacks=request.Callbacks.ToArray();
                    }
                    if(image!=null&&!dispatcher.HasShutdownStarted)
                    {
                        var result=image;
                        try{dispatcher.BeginInvoke(new Action(()=>{lock(gate){if(disposed)return;}foreach(var callback in callbacks)callback(result);}),DispatcherPriority.Background);}catch(InvalidOperationException){}
                    }
                }
            }
            finally{if(initialized>=0)CoUninitialize();}
        }
        public void Dispose()
        {
            lock(gate){if(disposed)return;disposed=true;pending.Clear();cache.Clear();order.Clear();requests.CompleteAdding();}
        }
    }
}
