using System;
using System.Collections.Generic;
using System.Threading;
using Autofac;
using kino;
using kino.Actors;
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
            var kino = new kino.kino();
            kino.SetResolver(new DependencyResolver(container));
            kino.Start();

            // Needed to let router bind to socket over INPROC. To be fixed by NetMQ in future.
            Thread.Sleep(TimeSpan.FromMilliseconds(30));

            //var actorHostManager = container.Resolve<IActorHostManager>();
            foreach (var actor in container.Resolve<IEnumerable<IActor>>())
            {
                kino.AssignActor(actor);
            }

            WriteLine("ActorHost started...");
            ReadLine();

            kino.Stop();
            container.Dispose();

            WriteLine("ActorHost stopped.");
        }
    }

    public class DependencyResolver : IDependencyResolver
    {
        private readonly IContainer container;

        public DependencyResolver(IContainer container)
        {
            this.container = container;
        }

        public T Resolve<T>()
        {
            try
            {
                return container.Resolve<T>();
            }
            catch (Exception)
            {
                return default(T);
            }
        }
    }
}