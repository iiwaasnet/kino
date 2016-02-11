namespace kino.Core.Sockets
{
    public interface ISocketFactory
    {
        ISocket CreateDealerSocket();
        ISocket CreateRouterSocket();
        ISocket CreateSubscriberSocket();
        ISocket CreatePublisherSocket();
    }
}