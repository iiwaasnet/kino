using System;

namespace rawf.Sockets
{
    public interface ISocketProvider : IDisposable
    {
        ISocket CreateDealerSocket();
        ISocket CreateRouterSocket();
    }
}