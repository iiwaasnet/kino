using System;
using System.Collections.Generic;
using System.Threading;
using Autofac;
using kino.Actors;
using kino.Core.Diagnostics;
using static System.Console;

namespace Server
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule<MainModule>();
            builder.RegisterModule<SecurityModule>();

            var container = builder.Build();
            var logger = container.Resolve<ILogger>();
            var kino = container.Resolve<kino.kino>();
            //kino.SetResolver(new DependencyResolver(container));
            kino.Start();

            // Needed to let router bind to socket over INPROC. To be fixed by NetMQ in future.
            for (var i = 0; i < 300; i++)
            {
                foreach (var actor in container.Resolve<IEnumerable<IActor>>())
                {
                    kino.AssignActor(actor);
                    logger.Debug($"Actor {actor.Identifier} registered");
                }
                Thread.Sleep(TimeSpan.FromMilliseconds(200));
            }

            logger.Debug("ActorHost started...");
            ReadLine();

            kino.Stop();
            container.Dispose();

            logger.Debug("ActorHost stopped.");
        }
    }
}