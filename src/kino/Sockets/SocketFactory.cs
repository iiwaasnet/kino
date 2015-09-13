using System;
using NetMQ;

namespace kino.Sockets
{
    public class SocketFactory : ISocketFactory
    {
        private readonly NetMQContext context;
        private readonly SocketConfiguration config;

        public SocketFactory(SocketConfiguration config)
        {
            context = NetMQContext.Create();
            this.config = config ?? CreateDefaultConfiguration();
        }

        public ISocket CreateDealerSocket()
            => new Socket(context.CreateDealerSocket(), config);

        public ISocket CreateRouterSocket()
            => new Socket(context.CreateRouterSocket(), config);

        public ISocket CreateSubscriberSocket()
            => new Socket(context.CreateSubscriberSocket(), config);

        public ISocket CreatePublisherSocket()
            => new Socket(context.CreatePublisherSocket(), config);

        public void Dispose()
            => context.Dispose();

        private SocketConfiguration CreateDefaultConfiguration()
            => new SocketConfiguration
               {
                   ReceivingHighWatermark = 1000,
                   SendingHighWatermark = 1000,
                   Linger = TimeSpan.Zero
               };
    }
}