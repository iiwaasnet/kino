using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Autofac;
using Client.Messages;
using kino;
using kino.Client;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Messaging;
using kino.Core.Sockets;
using static System.Console;

namespace Client
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new MainModule());
            var container = builder.Build();

            var componentResolver = new ComponentsResolver(container.ResolveOptional<SocketConfiguration>());

            var messageRouter = componentResolver.CreateMessageRouter(container.Resolve<RouterConfiguration>(),
                                                                      container.Resolve<ClusterMembershipConfiguration>(),
                                                                      container.Resolve<IEnumerable<RendezvousEndpoint>>(),
                                                                      container.Resolve<ILogger>());
            messageRouter.Start();
            // Needed to let router bind to socket over INPROC. To be fixed by NetMQ in future.
            Thread.Sleep(TimeSpan.FromMilliseconds(30));

            var messageHub = componentResolver.CreateMessageHub(container.Resolve<MessageHubConfiguration>(),
                                                                container.Resolve<ILogger>());
            messageHub.Start();

            Thread.Sleep(TimeSpan.FromSeconds(5));
            WriteLine($"Client is running... {DateTime.Now}");

            var request = Message.CreateFlowStartMessage(new HelloMessage {Greeting = Guid.NewGuid().ToString()});
            request.TraceOptions = MessageTraceOptions.None;
            var callbackPoint = CallbackPoint.Create<GroupCharsResponseMessage>();
            var promise = messageHub.EnqueueRequest(request, callbackPoint);
            if (promise.GetResponse().Wait(TimeSpan.FromSeconds(4)))
            {
                var response = promise.GetResponse().Result.GetPayload<GroupCharsResponseMessage>();

                WriteLine($"Text: {response.Text}");
                foreach (var groupInfo in response.Groups)
                {
                    WriteLine($"Char: {groupInfo.Char} - {groupInfo.Count} times");
                }
            }
            else
            {
                WriteLine($"Call timed out after {TimeSpan.FromSeconds(4).TotalSeconds} sec.");
            }

            ReadLine();
            messageHub.Stop();
            messageRouter.Stop();
            container.Dispose();

            WriteLine("Client stopped.");
        }
    }
}