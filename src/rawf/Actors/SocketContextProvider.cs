using System;
using NetMQ;

namespace rawf.Actors
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