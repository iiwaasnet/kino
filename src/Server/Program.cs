using System;
using System.Threading;
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

            var messageRouter = container.Resolve<IMessageRouter>();
            messageRouter.Start();
            Thread.Sleep(TimeSpan.FromMilliseconds(30));

            var ccMon = container.Resolve<IClusterMonitor>();
            ccMon.Start();

            var actorHost = container.Resolve<IActorHost>();
            actorHost.Start();
            actorHost.AssignActor(new Actor());

            Console.WriteLine("ActorHost started...");
            Console.ReadLine();

            actorHost.Stop();
            messageRouter.Stop();
            ccMon.Stop();

            Console.WriteLine("ActorHost stopped.");
        }
    }
}