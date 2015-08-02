using System;
using Autofac;
using rawf.Actors;
using rawf.Connectivity;

namespace Server
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new MainModule());
            var container = builder.Build();

            var ccMon = container.Resolve<IClusterConfigurationMonitor>();
            ccMon.Start();
            var messageRouter = container.Resolve<IMessageRouter>();
            messageRouter.Start();
            var actorHost = container.Resolve<IActorHost>();
            actorHost.AssignActor(new Actor());
            actorHost.Start();

            Console.WriteLine("ActorHost started...");
            Console.ReadLine();

            actorHost.Stop();
            messageRouter.Stop();
            ccMon.Stop();

            Console.WriteLine("ActorHost stopped.");
        }
    }
}