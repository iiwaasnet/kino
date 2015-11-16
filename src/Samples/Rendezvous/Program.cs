using Autofac;
using kino.Consensus.Configuration;
using kino.Core.Diagnostics;
using kino.Core.Sockets;
using kino.Rendezvous;
using kino.Rendezvous.Configuration;

namespace Rendezvous
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule<RendezvousModule>();
            var container = builder.Build();

            new ServiceHost(container.Resolve<LeaseConfiguration>(),
                            container.ResolveOptional<SocketConfiguration>(),
                            container.Resolve<RendezvousConfiguration>(),
                            container.Resolve<ApplicationConfiguration>(),
                            container.Resolve<ILogger>())
                .Run();
        }
    }
}