using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using rawf.Client;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
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
