using System;

namespace kino.Core.Sockets
{
    public interface ISocketFactory : IDisposable
    {
        ISocket CreateDealerSocket();
        ISocket CreateRouterSocket();
        ISocket CreateSubscriberSocket();
        ISocket CreatePublisherSocket();
    }
}