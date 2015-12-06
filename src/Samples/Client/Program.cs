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

            var componentResolver = new Composer(container.ResolveOptional<SocketConfiguration>());

            var messageRouter = componentResolver.BuildMessageRouter(container.Resolve<RouterConfiguration>(),
                                                                     container.Resolve<ClusterMembershipConfiguration>(),
                                                                     container.Resolve<IEnumerable<RendezvousEndpoint>>(),
                                                                     container.Resolve<ILogger>());
            messageRouter.Start();
            // Needed to let router bind to socket over INPROC. To be fixed by NetMQ in future.
            Thread.Sleep(TimeSpan.FromMilliseconds(300));

            var messageHub = componentResolver.BuildMessageHub(container.Resolve<MessageHubConfiguration>(),
                                                               container.Resolve<ILogger>());
            messageHub.Start();

            Thread.Sleep(TimeSpan.FromSeconds(5));
            WriteLine($"Client is running... {DateTime.Now}");
            var runs = 100000;

            while (true)
            {
                var promises = new List<IPromise>(runs);

                var timer = new Stopwatch();
                timer.Start();

                for (var i = 0; i < runs; i++)
                {
                    var request = Message.CreateFlowStartMessage(new HelloMessage {Greeting = Guid.NewGuid().ToString()});
                    request.TraceOptions = MessageTraceOptions.None;
                    var callbackPoint = CallbackPoint.Create<GroupCharsResponseMessage>();
                    promises.Add(messageHub.EnqueueRequest(request, callbackPoint));
                }

                var timeout = TimeSpan.FromSeconds(4);
                foreach (var promise in promises)
                {
                    using (promise)
                    {
                        if (promise.GetResponse().Wait(timeout))
                        {
                            promise.GetResponse().Result.GetPayload<GroupCharsResponseMessage>();

                            //WriteLine($"Text: {response.Text}");
                            //foreach (var groupInfo in response.Groups)
                            //{
                            //    WriteLine($"Char: {groupInfo.Char} - {groupInfo.Count} times");
                            //}
                        }
                        else
                        {
                            WriteLine($"Call timed out after {timeout.TotalSeconds} sec.");
                        }
                    }
                }

                timer.Stop();
                WriteLine($"Done {runs} times in {timer.ElapsedMilliseconds} ms");
            }

            ReadLine();
            messageHub.Stop();
            messageRouter.Stop();
            container.Dispose();

            WriteLine("Client stopped.");
        }
    }
}