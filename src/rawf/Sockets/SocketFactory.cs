using NetMQ;

namespace rawf.Sockets
{
    public class SocketFactory : ISocketFactory
    {
        private readonly NetMQContext context;

        public SocketFactory()
        {
            context = NetMQContext.Create();
        }

        public ISocket CreateDealerSocket()
        {
            return new Socket(context.CreateDealerSocket());
        }

        public ISocket CreateRouterSocket()
        {
            return new Socket(context.CreateRouterSocket());
        }

        public ISocket CreateSubscriberSocket()
        {
            return new Socket(context.CreateSubscriberSocket());
        }

        public void Dispose()
        {
            context.Dispose();
        }
    }
}