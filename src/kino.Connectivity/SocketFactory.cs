using kino.Messaging;
using NetMQ.Sockets;

namespace kino.Connectivity
{
    public class SocketFactory : ISocketFactory
    {
        private readonly IMessageWireFormatter messageWireFormatter;
        private readonly SocketConfiguration config;

        public SocketFactory(IMessageWireFormatter messageWireFormatter, SocketConfiguration config)
        {
            this.messageWireFormatter = messageWireFormatter;
            this.config = config;
        }

        public ISocket CreateDealerSocket()
            => new Socket(new DealerSocket(), messageWireFormatter, config);

        public ISocket CreateRouterSocket()
            => new Socket(new RouterSocket(), messageWireFormatter, config);

        public ISocket CreateSubscriberSocket()
            => new Socket(new SubscriberSocket(), messageWireFormatter, config);

        public ISocket CreatePublisherSocket()
            => new Socket(new PublisherSocket(), messageWireFormatter, config);

        public SocketConfiguration GetSocketConfiguration()
            => config;
    }
}