using System;
using System.Collections.Generic;
using System.Threading;
using Autofac;
using kino;
using kino.Actors;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Sockets;
using static System.Console;

namespace Server
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new MainModule());
            var container = builder.Build();

            var componentResolver = new Composer(container.ResolveOptional<SocketConfiguration>());

            var messageRouter = componentResolver.BuildMessageRouter(container.Resolve<RouterConfiguration>(),
                                                                      container.Resolve<ClusterMembershipConfiguration>(),
                                                                      container.Resolve<IEnumerable<RendezvousEndpoint>>(),
                                                                      container.Resolve<ILogger>());
            messageRouter.Start();
            // Needed to let router bind to socket over INPROC. To be fixed by NetMQ in future.
            Thread.Sleep(TimeSpan.FromMilliseconds(30));

            var actorHost = componentResolver.BuildActorHost(container.Resolve<RouterConfiguration>(),
                                                              container.Resolve<ILogger>());
            actorHost.Start();
            foreach (var actor in container.Resolve<IEnumerable<IActor>>())
            {
                actorHost.AssignActor(actor);
            }

            WriteLine("ActorHost started...");
            ReadLine();

            actorHost.Stop();
            messageRouter.Stop();
            container.Dispose();

            WriteLine("ActorHost stopped.");
        }
    }
}