using System;

namespace rawf.Sockets
{
    public interface ISocketFactory : IDisposable
    {
        ISocket CreateDealerSocket();
        ISocket CreateRouterSocket();
        ISocket CreateSubscriberSocket();
        ISocket CreatePublisherSocket();
    }
}