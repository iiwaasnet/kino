using System;
using System.Threading;
using NetMQ;

namespace kino.Connectivity
{
    public static class SocketHelper
    {
        internal static void SafeConnect(Action action)
        {
            var retries = 3;
            while (retries > 0)
            {
                try
                {
                    action();
                    return;
                }
                catch (EndpointNotFoundException)
                {
                    retries--;
                    Thread.Sleep(TimeSpan.FromMilliseconds(100));
                }
            }
        }
    }
}