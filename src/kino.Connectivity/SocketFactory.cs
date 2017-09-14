using NetMQ.Sockets;

namespace kino.Connectivity
{
    public class SocketFactory : ISocketFactory
    {
        private readonly SocketConfiguration config;

        public SocketFactory(SocketConfiguration config)
            => this.config = config;

        public ISocket CreateDealerSocket()
            => new Socket(new DealerSocket(), config);

        public ISocket CreateRouterSocket()
            => new Socket(new RouterSocket(), config);

        public ISocket CreateSubscriberSocket()
            => new Socket(new SubscriberSocket(), config);

        public ISocket CreatePublisherSocket()
            => new Socket(new PublisherSocket(), config);

        public SocketConfiguration GetSocketConfiguration()
            => config;
    }
}