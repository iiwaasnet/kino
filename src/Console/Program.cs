using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            var context = (NetMQContext) new SocketContextProvider().GetContext();

            var messageRouter = new MessageRouter(context);
            messageRouter.Start();

            Thread.Sleep(TimeSpan.FromSeconds(1));

            var actors = CreateActors(context, 1).ToList();

            Thread.Sleep(TimeSpan.FromSeconds(1));

            var messageHub = new MessageHub(context);
            messageHub.Start();

            var client = new Client(messageHub);
            var callbackPoint = new CallbackPoint(EhlloMessage.MessageIdentity);

            RunTest(client, callbackPoint);

            actors.ForEach(a => a.Stop());
            messageHub.Stop();
            messageRouter.Stop();
            context.Dispose();
        }

        private static void RunTest(Client client, CallbackPoint callbackPoint)
        {
            var timer = new Stopwatch();
            timer.Start();

            var responses = new List<Task<IMessage>>();
            for (var i = 0; i < 100000; i++)
            {
                var message = Message.CreateFlowStartMessage(new HelloMessage {Greeting = "Hello"}, HelloMessage.MessageIdentity);
                responses.Add(client.Send(message, callbackPoint).GetResponse());
                //var msg = response.GetPayload<EhlloMessage>();

                //System.Console.WriteLine($"{i} Received: {msg.Ehllo}");
            }

            responses.ForEach(r => r.Wait());

            timer.Stop();

            System.Console.WriteLine($"Done in {timer.ElapsedMilliseconds} msec");
        }

        private static IEnumerable<ActorHost> CreateActors(NetMQContext context, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var actorHost = new ActorHost(context);
                actorHost.Start();
                var actor = new Actor();
                actorHost.AssignActor(actor);
                yield return actorHost;
            }
        }
    }
}