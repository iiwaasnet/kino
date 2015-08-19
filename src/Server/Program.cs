using System;
using System.Collections.Generic;
using System.Threading;
using Autofac;
using rawf.Actors;
using rawf.Connectivity;
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

      var messageRouter = container.Resolve<IMessageRouter>();
      messageRouter.Start();
      Thread.Sleep(TimeSpan.FromMilliseconds(30));

      var ccMon = container.Resolve<IClusterMonitor>();
      ccMon.Start();

      var actorHost = container.Resolve<IActorHost>();
      actorHost.Start();
      foreach (var actor in container.Resolve<IEnumerable<IActor>>())
      {
        actorHost.AssignActor(actor);
      }


      WriteLine("ActorHost started...");
      ReadLine();

      actorHost.Stop();
      messageRouter.Stop();
      ccMon.Stop();

      WriteLine("ActorHost stopped.");
    }
  }
}