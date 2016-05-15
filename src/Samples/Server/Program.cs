using System;
using System.Collections.Generic;
using System.Threading;
using Autofac;
using Autofac.kino;
using kino.Actors;
using kino.Core.Connectivity;
using static System.Console;

namespace Server
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule<MainModule>();
            builder.RegisterModule<KinoModule>();
            var container = builder.Build();

            var messageRouter = container.Resolve<IMessageRouter>();
            messageRouter.Start(TimeSpan.FromSeconds(3));
            // Needed to let router bind to socket over INPROC. To be fixed by NetMQ in future.
            Thread.Sleep(TimeSpan.FromMilliseconds(30));

            var actorHostManager = container.Resolve<IActorHostManager>();
            foreach (var actor in container.Resolve<IEnumerable<IActor>>())
            {
                actorHostManager.AssignActor(actor);
            }

            WriteLine("ActorHost started...");
            ReadLine();

            actorHostManager.Dispose();
            messageRouter.Stop();
            container.Dispose();

            WriteLine("ActorHost stopped.");
        }
    }
}