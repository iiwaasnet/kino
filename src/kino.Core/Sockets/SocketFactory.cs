using System;
using NetMQ.Sockets;

namespace kino.Core.Sockets
{
    public class SocketFactory : ISocketFactory
    {
        private readonly SocketConfiguration config;

        public SocketFactory(SocketConfiguration config)
        {
            this.config = config ?? CreateDefaultConfiguration();
        }

        public ISocket CreateDealerSocket()
            => new Socket(new DealerSocket(), config);

        public ISocket CreateRouterSocket()
            => new Socket(new RouterSocket(), config);

        public ISocket CreateSubscriberSocket()
            => new Socket(new SubscriberSocket(), config);

        public ISocket CreatePublisherSocket()
            => new Socket(new PublisherSocket(),  config);

        private SocketConfiguration CreateDefaultConfiguration()
            => new SocketConfiguration
               {
                   ReceivingHighWatermark = 1000,
                   SendingHighWatermark = 1000,
                   Linger = TimeSpan.Zero
               };
    }
}