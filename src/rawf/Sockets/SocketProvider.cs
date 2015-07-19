using NetMQ;

namespace rawf.Sockets
{
    public class SocketProvider : ISocketProvider
    {
        private readonly NetMQContext context;

        public SocketProvider()
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

        public void Dispose()
        {
            context.Dispose();
        }
    }
}