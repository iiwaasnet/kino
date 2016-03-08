using System;
using System.Threading;

namespace kino.Core.Framework
{
    public static class ThreadExtensions
    {
        public static void Sleep(this TimeSpan delay)
        {
            if (delay.TotalMilliseconds > 0)
            {
                using (var @lock = new ManualResetEvent(false))
                {
                    @lock.WaitOne(delay);
                }
            }
        }
    }
}