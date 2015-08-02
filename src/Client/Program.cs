using System;
using Autofac;
using Client.Messages;
using rawf.Client;
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

            var messageHub = container.Resolve<IMessageHub>();
            
            Console.WriteLine("Client is running...");

            var message = Message.CreateFlowStartMessage(new HelloMessage { Greeting = "Hello world!" }, HelloMessage.MessageIdentity);
            var callback = new CallbackPoint(EhlloMessage.MessageIdentity);
            var promise = messageHub.EnqueueRequest(message, callback);
            var resp = promise.GetResponse().Result.GetPayload<EhlloMessage>();
            Console.WriteLine(resp.Ehllo);
            
            Console.ReadLine();
        }
    }
}