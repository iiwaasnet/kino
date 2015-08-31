using System;

namespace kino.Sockets
{
    public interface ISocketFactory : IDisposable
    {
        ISocket CreateDealerSocket();
        ISocket CreateRouterSocket();
        ISocket CreateSubscriberSocket();
        ISocket CreatePublisherSocket();
    }
}