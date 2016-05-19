using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using Autofac;
using Autofac.kino;
using Client.Messages;
using kino;
using kino.Client;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Sockets;
using static System.Console;

namespace Client
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule<MainModule>();
            builder.RegisterModule<KinoModule>();
            var container = builder.Build();

            var logger = container.Resolve<ILogger>();
            var messageRouter = container.Resolve<IMessageRouter>();
            messageRouter.Start(TimeSpan.FromSeconds(3));
            // Needed to let router bind to socket over INPROC. To be fixed by NetMQ in future.
            //Thread.Sleep(TimeSpan.FromMilliseconds(300));

            var messageHub = container.Resolve<IMessageHub>();
            messageHub.Start();

            Thread.Sleep(TimeSpan.FromSeconds(5));
            logger.Trace($"Client is running... {DateTime.Now}");
            var runs = 10;

            var receiverIdentity = FindReceiver(messageHub);

            while (true)
            {
                var promises = new List<IPromise>(runs);

                var timer = new Stopwatch();
                timer.Start();

                for (var i = 0; i < runs; i++)
                {
                    var request = Message.CreateFlowStartMessage(new HelloMessage {Greeting = Guid.NewGuid().ToString()});
                    request.TraceOptions = MessageTraceOptions.None;
                    request.SetReceiverNode(receiverIdentity);
                    var callbackPoint = CallbackPoint.Create<GroupCharsResponseMessage>();
                    promises.Add(messageHub.EnqueueRequest(request, callbackPoint));
                }

                var timeout = TimeSpan.FromMilliseconds(400);
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
                            logger.Warn($"{DateTime.UtcNow} Call timed out after {timeout.TotalSeconds} sec.");
                        }
                    }
                }

                timer.Stop();

                var messagesPerTest = 3;
                var performance = (timer.ElapsedMilliseconds > 0)
                                      ? ((messagesPerTest * runs) / (double) timer.ElapsedMilliseconds * 1000).ToString("##.00")
                                      : "Infinite";
                logger.Trace($"Done {runs} times in {timer.ElapsedMilliseconds} ms with {performance} msg/sec");

                Thread.Sleep(TimeSpan.FromMilliseconds(50));
            }

            ReadLine();
            messageHub.Stop();
            messageRouter.Stop();
            container.Dispose();

            WriteLine("Client stopped.");
        }

        private static SocketIdentifier FindReceiver(IMessageHub messageHub)
        {
            var request = Message.CreateFlowStartMessage(new RequestKnownMessageRoutesMessage());
            var callback = CallbackPoint.Create<KnownMessageRoutesMessage>();
            using (var promise = messageHub.EnqueueRequest(request, callback))
            {
                var response = promise.GetResponse().Result;
                var registeredRoutes = response.GetPayload<KnownMessageRoutesMessage>();

                return new SocketIdentifier(registeredRoutes.InternalRoutes.SocketIdentity);
            }

        }
    }
}