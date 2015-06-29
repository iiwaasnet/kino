using System;
using System.Diagnostics;
using System.Threading;
using Console.Messages;
using NetMQ;

namespace Console
{
    internal class Program
    {
        internal const string EndpointAddress = "tcp://127.0.0.1:5555";
        //TODO: Switch to inproc protocol after https://github.com/zeromq/netmq/pull/343 is released 
        //internal const string EndpointAddress = "inproc://local";

        private static void Main(string[] args)
        {
            var context = NetMQContext.Create();
            var messageRouter = new MessageRouter(context);
            messageRouter.Start();

            Thread.Sleep(TimeSpan.FromSeconds(1));

            var actorHost = new ActorHost(context);
            actorHost.Start();
            var actor = new Actor();
            actorHost.AssignActor(actor);

            Thread.Sleep(TimeSpan.FromSeconds(1));

            var messageHub = new MessageHub(context);
            messageHub.Start();

            var timer = new Stopwatch();
            timer.Start();

            var client = new Client(messageHub);
            var callbackPoint = new CallbackPoint(EhlloMessage.MessageIdentity);

            var message = Message.CreateFlowStartMessage(new HelloMessage {Greeting = "Hello"}, HelloMessage.MessageIdentity);
            var response = client.Send(message, callbackPoint).GetResponse().Result;
            var msg = response.GetPayload<EhlloMessage>();

            System.Console.WriteLine($"Received: {msg.Ehllo}");
            timer.Stop();

            System.Console.WriteLine($"Done in {timer.ElapsedMilliseconds} msec");

            actorHost.Stop();
            messageHub.Stop();
            messageRouter.Stop();
            context.Dispose();
        }
    }
}