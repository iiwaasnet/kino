using NetMQ;

namespace kino.Sockets
{
    public class SocketFactory : ISocketFactory
    {
        private readonly NetMQContext context;

        public SocketFactory()
        {
            context = NetMQContext.Create();
        }

        public ISocket CreateDealerSocket()
            => new Socket(context.CreateDealerSocket());

        public ISocket CreateRouterSocket()
            => new Socket(context.CreateRouterSocket());

        public ISocket CreateSubscriberSocket()
            => new Socket(context.CreateSubscriberSocket());

        public ISocket CreatePublisherSocket()
            => new Socket(context.CreatePublisherSocket());

        public void Dispose()
            => context.Dispose();
    }
}