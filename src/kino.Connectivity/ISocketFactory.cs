namespace kino.Connectivity
{
    public interface ISocketFactory
    {
        ISocket CreateDealerSocket();

        ISocket CreateRouterSocket();

        ISocket CreateSubscriberSocket();

        ISocket CreatePublisherSocket();

        SocketConfiguration GetSocketDefaultConfiguration();
    }
}