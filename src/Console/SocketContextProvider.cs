using System;
using NetMQ;

namespace Console
{
    public class SocketContextProvider : ISocketContextProvider
    {
        private readonly NetMQContext context;

        public SocketContextProvider()
        {
            context = NetMQContext.Create();
        }

        public IDisposable GetContext()
        {
            return context;
        }
    }
}