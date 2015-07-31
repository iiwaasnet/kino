using System;
using Autofac;

namespace rawf.Rendezvous
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new MainModule());
            var container = builder.Build();

            var service = container.Resolve<IRendezvousService>();
            service.Start();

            Console.WriteLine("Service started...");
            Console.ReadLine();

            Console.WriteLine("Service stopped.");
            service.Stop();
        }
    }
}