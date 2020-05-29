using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Swiddler.Common
{
    public class DelayedInvoker : IDisposable
    {
        public int Delay { get; set; } = 100; // milliseconds

        readonly object syncObj = new object();
        readonly Stopwatch stopwatch = Stopwatch.StartNew();

        Action action = null;
        Task delayedTask = null;
        bool disposed;

        public void Queue(Action action)
        {
            lock (syncObj)
            {
                if (disposed) return; // disposed
                this.action = action;
                if (delayedTask != null) return; // no need to call Flush
            }
            Flush(null);
        }

        void Flush(Task task)
        {
            if (stopwatch.ElapsedMilliseconds < Delay)
            {
                lock (syncObj)
                {
                    if (delayedTask == null)
                        delayedTask = Task.Delay(Delay).ContinueWith(Flush);
                }
                return;
            }

            while (stopwatch.ElapsedMilliseconds >= Delay)
            {
                stopwatch.Restart();

                Action oldAction;
                lock (syncObj)
                {
                    if (disposed) return; // disposed
                    delayedTask = null;
                    oldAction = action;
                    action = null;
                }

                oldAction?.Invoke();
            }
        }

        public void Dispose()
        {
            lock (syncObj)
            {
                disposed = true;
                action = null;
            }
        }
    }
}
