using System;
using System.Threading;
using Console.Messages;
using NetMQ;

namespace Console
{
    internal class Program
    {
        internal const string EndpointAddress = "tcp://127.0.0.1:5555";
        //internal const string EndpointAddress = "inproc://local";

        private static void Main(string[] args)
        {
            var messageRouter = new MessageRouter(NetMQContext.Create());
            messageRouter.Start();

            Thread.Sleep(TimeSpan.FromSeconds(5));

            var actorHost = new ActorHost(NetMQContext.Create());
            actorHost.Start();
            var actor = new Actor();
            actorHost.AssignActor(actor);

            Thread.Sleep(TimeSpan.FromSeconds(5));

            var requestSink = new ClientRequestSink(NetMQContext.Create());
            requestSink.Start();
            var client = new Client(requestSink);

            var callbackPoint = client.CreateCallbackPoint(EhlloMessage.MessageIdentity);
            var message = Message.CreateFlowStartMessage(new HelloMessage {Greeting = "Hello"}, HelloMessage.MessageIdentity);
            message.RegisterCallbackPoint(callbackPoint);
            var response = client.Send(message).GetResponse().Result;
            var msg = response.GetPayload<EhlloMessage>();

            System.Console.WriteLine($"Received: {msg.Ehllo}");

            actorHost.Stop();
            requestSink.Stop();
            messageRouter.Stop();
        }
    }

    internal class Client
    {
        private readonly ClientRequestSink requestSink;

        public Client(ClientRequestSink requestSink)
        {
            this.requestSink = requestSink;
        }

        public ICallbackPoint CreateCallbackPoint(string messageIdentity)
        {
            return new CallbackPoint
                   {
                       MessageIdentity = messageIdentity,
                       ReceiverIdentity = Guid.NewGuid().ToString()
                   };
        }

        public IPromise Send(IMessage message)
        {
            return requestSink.EnqueueRequest(message);
        }
    }
}