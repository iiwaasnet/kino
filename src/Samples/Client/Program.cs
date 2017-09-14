using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autofac;
using Client.Messages;
using kino.Client;
using kino.Core;
using kino.Messaging;
using kino.Messaging.Messages;
using static System.Console;

namespace Client
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule<MainModule>();
            builder.RegisterModule<SecurityModule>();
            var container = builder.Build();
            //var logger = container.Resolve<ILogger>();
            var kino = container.Resolve<kino.kino>();
            //kino.SetResolver(new DependencyResolver(container));
            kino.Start();

            // Needed to let router bind to socket over INPROC. To be fixed by NetMQ in future.

            var messageHub = kino.GetMessageHub();
            messageHub.Start();

            WriteLine($"Client is running... {DateTime.Now}");
            var runs = 10000;

            var messageIdentifier = MessageIdentifier.Create<HelloMessage>();
            var routesRequest = Message.CreateFlowStartMessage(new RequestMessageExternalRoutesMessage
                                                               {
                                                                   MessageContract = new MessageContract
                                                                                     {
                                                                                         Identity = messageIdentifier.Identity,
                                                                                         Version = messageIdentifier.Version,
                                                                                         Partition = messageIdentifier.Partition
                                                                                     }
                                                               });
            //var response = messageHub.EnqueueRequest(routesRequest, CallbackPoint.Create<MessageExternalRoutesMessage>());
            //var route = response.GetResponse().Result.GetPayload<MessageExternalRoutesMessage>().Routes.First();

            //Thread.Sleep(TimeSpan.FromSeconds(5));
            while (true)
            {
                var promises = new List<IPromise>(runs);

                var timer = new Stopwatch();
                timer.Start();

                for (var i = 0; i < runs; i++)
                {
                    var request = Message.CreateFlowStartMessage(new HelloMessage {Greeting = Guid.NewGuid().ToString()});
                    request.TraceOptions = MessageTraceOptions.None;
                    //request.SetReceiverActor(new ReceiverIdentifier(route.NodeIdentity), new ReceiverIdentifier(route.ReceiverIdentity.First()));
                    //request.SetReceiverNode(new ReceiverIdentifier(route.NodeIdentity));
                    var callbackPoint = CallbackPoint.Create<EhlloMessage>();
                    promises.Add(messageHub.EnqueueRequest(request, callbackPoint));
                }

                var timeout = TimeSpan.FromMilliseconds(4000);
                foreach (var promise in promises)
                {
                    using (promise)
                    {
                        if (promise.GetResponse().Wait(timeout))
                        {
                            promise.GetResponse().Result.GetPayload<EhlloMessage>();
                        }
                        else
                        {
                            var fc = ForegroundColor;
                            ForegroundColor = ConsoleColor.Yellow;
                            WriteLine($"{DateTime.UtcNow} Call timed out after {timeout.TotalSeconds} sec.");
                            ForegroundColor = fc;
                            promises.ForEach(p => p.Dispose());
                            break;
                        }
                    }
                }

                timer.Stop();

                var messagesPerTest = 2;
                var performance = (timer.ElapsedMilliseconds > 0)
                                      ? ((messagesPerTest * runs) / (double) timer.ElapsedMilliseconds * 1000).ToString("##.00")
                                      : "Infinite";
                WriteLine($"Done {runs} times in {timer.ElapsedMilliseconds} ms with {performance} msg/sec");

                //Thread.Sleep(TimeSpan.FromSeconds(2));
            }

            ReadLine();
            messageHub.Stop();
            kino.Stop();
            container.Dispose();

            WriteLine("Client stopped.");
        }
    }
}