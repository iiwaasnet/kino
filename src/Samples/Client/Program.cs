using System;
using System.Collections.Generic;
using System.Threading;
using Autofac;
using Autofac.kino;
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
            builder.RegisterModule<MainModule>();
            builder.RegisterModule<KinoModule>();
            var container = builder.Build();

            var messageRouter = container.Resolve<IMessageRouter>();
            messageRouter.Start();
            // Needed to let router bind to socket over INPROC. To be fixed by NetMQ in future.
            Thread.Sleep(TimeSpan.FromMilliseconds(300));

            var messageHub = container.Resolve<IMessageHub>();
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