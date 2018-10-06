using Autofac;
using System;

namespace kino.Rendezvous.Service
{
    public class Program
    {
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(5);

        static void Main(string[] args)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule<MainModule>();
            var container = builder.Build();

            var rendezvousService = container.Resolve<IRendezvousService>();
            try
            {
                if (!rendezvousService.Start(StartTimeout))
                {
                    throw new Exception($"Failed starting RendezvousService after {StartTimeout.TotalMilliseconds} ms!");
                }

                Console.WriteLine("Press any key to stop...");
                Console.ReadLine();
            }
            finally
            {
                rendezvousService.Stop();
            }
        }
    }
}