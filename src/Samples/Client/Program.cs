using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Autofac;
using Client.Messages;
using kino;
using kino.Client;
using kino.Core;
using kino.Core.Framework;
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
            var kino = new kino.kino();
            kino.SetResolver(new DependencyResolver(container));
            kino.Start();

            // Needed to let router bind to socket over INPROC. To be fixed by NetMQ in future.
            Thread.Sleep(TimeSpan.FromMilliseconds(300));

            var messageHub = kino.GetMessageHub();
            messageHub.Start();

            Thread.Sleep(TimeSpan.FromSeconds(5));
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
            var response = messageHub.EnqueueRequest(routesRequest, CallbackPoint.Create<MessageExternalRoutesMessage>());
            response.GetResponse().Wait();
            var route = response.GetResponse().Result.GetPayload<MessageExternalRoutesMessage>().Routes.First();

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
                    var callbackPoint = CallbackPoint.Create<GroupCharsResponseMessage>();
                    promises.Add(messageHub.EnqueueRequest(request, callbackPoint));
                }

                var timeout = TimeSpan.FromMilliseconds(4000);
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

                var messagesPerTest = 3;
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

        private static ReceiverIdentifier FindReceiver(IMessageHub messageHub)
        {
            var request = Message.CreateFlowStartMessage(new RequestKnownMessageRoutesMessage());
            var callback = CallbackPoint.Create<KnownMessageRoutesMessage>();
            using (var promise = messageHub.EnqueueRequest(request, callback))
            {
                var response = promise.GetResponse().Result;
                var registeredRoutes = response.GetPayload<KnownMessageRoutesMessage>();

                return new ReceiverIdentifier(registeredRoutes.InternalRoutes.SocketIdentity);
            }
        }
    }

    public class DependencyResolver : IDependencyResolver
    {
        private readonly IContainer container;

        public DependencyResolver(IContainer container)
        {
            this.container = container;
        }

        public T Resolve<T>()
        {
            try
            {
                return container.Resolve<T>();
            }
            catch (Exception)
            {
                return default(T);
            }
        }
    }
}