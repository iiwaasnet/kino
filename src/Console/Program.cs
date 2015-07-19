using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Console.Messages;
using rawf.Actors;
using rawf.Client;
using rawf.Connectivity;
using rawf.Messaging;
using rawf.Sockets;

namespace Console
{
    internal class Program
    {
        //internal const string LocalEndpointAddress1 = "inproc://loc1";
        internal const string LocalEndpointAddress1 = "tcp://127.0.0.1:5555";
        internal const string PeerEndpointAddress1 = "tcp://127.0.0.1:5554";

        //internal const string LocalEndpointAddress2 = "inproc://loc2";
        internal const string LocalEndpointAddress2 = "tcp://127.0.0.1:4555";
        internal const string PeerEndpointAddress2 = "tcp://127.0.0.1:4554";
        //TODO: Switch to inproc protocol after https://github.com/zeromq/netmq/pull/343 is released 
        //internal const string EndpointAddress = "inproc://local";

        private static void Main(string[] args)
        {
            //IMessageSerializer messageSerializer;
            //messageSerializer = new NewtonJsonMessageSerializer();
            //Serialize(messageSerializer, "Newton");
            //Serialize(messageSerializer, "Newton");

            //messageSerializer = new ProtobufMessageSerializer();
            //Serialize(messageSerializer, "Protobuf");
            //Serialize(messageSerializer, "Protobuf");


            StartProcessingNode();
            StartSendingNode();

            System.Console.ReadLine();
        }

        private static void Serialize(IMessageSerializer messageSerializer, string test)
        {
            var msg = new HelloMessage {Greeting = new string('w', 500)};
            var times = 10000;

            var timer = new Stopwatch();
            timer.Start();
            for (var i = 0; i < times; i++)
            {
                messageSerializer.Deserialize<HelloMessage>(messageSerializer.Serialize(msg));
            }
            timer.Stop();

            System.Console.WriteLine($"{test}: {timer.ElapsedMilliseconds}");
        }

        private static void StartProcessingNode()
        {
            var routerConfig = new ConnectivityConfiguration(LocalEndpointAddress2, PeerEndpointAddress2, PeerEndpointAddress1);
            var connectivityProvider = new ConnectivityProvider(new SocketProvider(), routerConfig);

            var messageRouter = new MessageRouter(connectivityProvider, new MessageHandlerStack());
            messageRouter.Start();

            var actors = CreateActors(connectivityProvider, 1).ToList();

            //System.Console.WriteLine("Press ENTER to stop");
            //actors.ToList().ForEach(a => a.Stop());
            //System.Console.ReadLine();
            //messageRouter.Stop();
            //connectivityProvider.Dispose();
        }

        private static void StartSendingNode()
        {
            
            var routerConfig = new ConnectivityConfiguration(LocalEndpointAddress1, PeerEndpointAddress1, PeerEndpointAddress2);
            var connectivityProvider = new ConnectivityProvider(new SocketProvider(), routerConfig);
            var messageRouter = new MessageRouter(connectivityProvider, new MessageHandlerStack());
            messageRouter.Start();

            var messageHub = new MessageHub(connectivityProvider);
            messageHub.Start();

            var client = new Client(messageHub);
            var callbackPoint = new CallbackPoint(EhlloMessage.MessageIdentity);

            //Thread.Sleep(TimeSpan.FromSeconds(1));

            RunTest(client, callbackPoint);

            //System.Console.WriteLine("Press ENTER to stop");
            //System.Console.ReadLine();
            //messageHub.Stop();
            //messageRouter.Stop();
            //connectivityProvider.Dispose();
        }

        private static void RunTest(Client client, CallbackPoint callbackPoint)
        {
            var timer = new Stopwatch();
            timer.Start();

            var responses = new List<Task<IMessage>>();
            for (var i = 0; i < 1; i++)
            {
                var message = Message.CreateFlowStartMessage(new HelloMessage {Greeting = "Hello"}, HelloMessage.MessageIdentity);
                responses.Add(client.Send(message, callbackPoint).GetResponse());
            }

            responses.ForEach(r =>
                              {
                                  try
                                  {
                                      r.Wait();
                                      var msg = r.Result.GetPayload<EhlloMessage>();
                                      System.Console.WriteLine($"Received: {msg.Ehllo}");
                                  }
                                  catch (Exception err)
                                  {
                                      System.Console.WriteLine($"Error happened: {err.ToString()}");
                                  }
                              });

            timer.Stop();

            System.Console.WriteLine($"Done in {timer.ElapsedMilliseconds} msec");
        }

        private static IEnumerable<IActorHost> CreateActors(IConnectivityProvider connectivityProvider, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var actorHost = new ActorHost(new ActorHandlersMap(), new MessagesCompletionQueue(), connectivityProvider);
                var actor = new Actor();
                actorHost.AssignActor(actor);
                actorHost.Start();

                yield return actorHost;
            }
        }

        //private static void Main(string[] args)
        //{
        //    var runnerIndex = int.Parse(args[0]);
        //    System.Console.WriteLine($"RunnerIndex {runnerIndex}");

        //    ConnectivityProvider connectivityProvider;
        //    if (runnerIndex == 1)
        //    {
        //        connectivityProvider = new ConnectivityProvider(LocalEndpointAddress1, PeerEndpointAddress1, PeerEndpointAddress2);
        //    }
        //    else
        //    {
        //        connectivityProvider = new ConnectivityProvider(LocalEndpointAddress2, PeerEndpointAddress2, PeerEndpointAddress1);
        //    }

        //    var messageRouter = new MessageRouter(connectivityProvider);
        //    messageRouter.Start();

        //    IEnumerable<IActorHost> actors = null;
        //    if (runnerIndex == 2)
        //    {
        //        actors = CreateActors(connectivityProvider, 1).ToList();
        //    }
        //    var messageHub = new MessageHub(connectivityProvider);
        //    messageHub.Start();
        //    if (runnerIndex == 1)
        //    {
        //        var client = new Client(messageHub);
        //        var callbackPoint = new CallbackPoint(EhlloMessage.MessageIdentity);

        //        Thread.Sleep(TimeSpan.FromSeconds(1));

        //        RunTest(client, callbackPoint);
        //    }
        //    if (runnerIndex == 2)
        //    {
        //        actors.ToList().ForEach(a => a.Stop());
        //    }

        //    System.Console.WriteLine("Press ENTER to stop");
        //    System.Console.ReadLine();
        //    messageHub.Stop();
        //    messageRouter.Stop();
        //    connectivityProvider.Dispose();
        //}
    }
}