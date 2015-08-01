using System;
using Autofac;

namespace rawf.Actors
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new MainModule());
            var container = builder.Build();

            var actorHost = container.Resolve<IActorHost>();
            actorHost.Start();

            Console.WriteLine("ActorHost started...");
            Console.ReadLine();

            actorHost.Stop();

            Console.WriteLine("ActorHost stopped.");
        }
    }
}