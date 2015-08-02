using System;
using Autofac;
using rawf.Client;

namespace Client
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new MainModule());
            var container = builder.Build();

            var messageHub = container.Resolve<IMessageHub>();

            Console.WriteLine("Client is running...");
            Console.ReadLine();
            Console.WriteLine("Client stopped.");
        }
    }
}