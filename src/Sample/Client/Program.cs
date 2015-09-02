using System;
using System.Threading;
using Autofac;
using Client.Messages;
using kino.Client;
using kino.Connectivity;
using kino.Messaging;
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

            var messageRouter = container.Resolve<IMessageRouter>();
            messageRouter.Start();
            Thread.Sleep(TimeSpan.FromMilliseconds(30));

            var ccMon = container.Resolve<IClusterMonitor>();
            ccMon.Start();
            var messageHub = container.Resolve<IMessageHub>();
            messageHub.Start();

            Thread.Sleep(TimeSpan.FromSeconds(2));
            WriteLine($"Client is running... {DateTime.Now}");

            var request = Message.CreateFlowStartMessage(new HelloMessage {Greeting = Guid.NewGuid().ToString()}, HelloMessage.MessageIdentity);
            request.TraceOptions = MessageTraceOptions.None;
            var callbackPoint = new CallbackPoint(GroupCharsResponseMessage.MessageIdentity);
            var promise = messageHub.EnqueueRequest(request, callbackPoint);
            var response = promise.GetResponse().Result.GetPayload<GroupCharsResponseMessage>();

            WriteLine($"Text: {response.Text}");
            foreach (var groupInfo in response.Groups)
            {
                WriteLine($"Char: {groupInfo.Char} - {groupInfo.Count} times");
            }

            ReadLine();
            messageHub.Stop();
            messageRouter.Stop();
            ccMon.Stop();
            container.Dispose();
        }
    }
}