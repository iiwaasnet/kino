using System;
using System.Threading;
using Autofac;
using Client.Messages;
using rawf.Client;
using rawf.Connectivity;
using rawf.Messaging;

namespace Client
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
            var messageHub = container.Resolve<IMessageHub>();
            messageHub.Start();

            Thread.Sleep(TimeSpan.FromSeconds(20));
            
            Console.WriteLine("Client is running...");

            var message = Message.CreateFlowStartMessage(new HelloMessage { Greeting = "Hello world!" }, HelloMessage.MessageIdentity);
            var callback = new CallbackPoint(EhlloMessage.MessageIdentity);
            var promise = messageHub.EnqueueRequest(message, callback);
            var resp = promise.GetResponse().Result.GetPayload<EhlloMessage>();
            Console.WriteLine(resp.Ehllo);
            
            Console.ReadLine();
            messageHub.Stop();
            messageRouter.Stop();
            ccMon.Stop();
        }
    }
}