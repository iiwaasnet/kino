using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Autofac;
using Autofac.kino;
using Client.Messages;
using kino.Client;
using kino.Core;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using kino.Security;
using static System.Console;

namespace Client
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            //TryEncryptDecrypt();
            ////SignFile(Guid.NewGuid().ToByteArray(), @"C:\0\lps\logs\2016-06-09.log", @"c:\devnull");
            ////SignFile(Guid.NewGuid().ToByteArray(), @"C:\0\lps\logs\2016-06-09.log", @"c:\devnull");
            //return;

            var builder = new ContainerBuilder();
            builder.RegisterModule<MainModule>();
            builder.RegisterModule<KinoModule>();
            builder.RegisterModule<SecurityModule>();
            var container = builder.Build();

            var messageRouter = container.Resolve<IMessageRouter>();
            messageRouter.Start(TimeSpan.FromSeconds(3));
            // Needed to let router bind to socket over INPROC. To be fixed by NetMQ in future.
            Thread.Sleep(TimeSpan.FromMilliseconds(300));

            var messageHub = container.Resolve<IMessageHub>();
            messageHub.Start();

            Thread.Sleep(TimeSpan.FromSeconds(5));
            WriteLine($"Client is running... {DateTime.Now}");
            var runs = 1000;

            //var receiverIdentity = FindReceiver(messageHub);

            var securityProvider = container.Resolve<ISecurityProvider>();
            var helloMessageIdentity = MessageIdentifier.Create<HelloMessage>();
            while (true)
            {
                var promises = new List<IPromise>(runs);

                var timer = new Stopwatch();
                timer.Start();

                for (var i = 0; i < runs; i++)
                {
                    var request = Message.CreateFlowStartMessage(new HelloMessage {Greeting = Guid.NewGuid().ToString()},
                                                                 securityProvider.GetDomain(helloMessageIdentity.Identity));
                    request.TraceOptions = MessageTraceOptions.None;
                    //request.SetReceiverNode(receiverIdentity);
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
                            WriteLine($"{DateTime.UtcNow} Call timed out after {timeout.TotalSeconds} sec.");
                        }
                    }
                }

                timer.Stop();

                var messagesPerTest = 3;
                var performance = (timer.ElapsedMilliseconds > 0)
                                      ? ((messagesPerTest * runs) / (double) timer.ElapsedMilliseconds * 1000).ToString("##.00")
                                      : "Infinite";
                WriteLine($"Done {runs} times in {timer.ElapsedMilliseconds} ms with {performance} msg/sec");
            }

            ReadLine();
            messageHub.Stop();
            messageRouter.Stop();
            container.Dispose();

            WriteLine("Client stopped.");
        }

        //private static void TryEncryptDecrypt()
        //{
        //    var securityProvider = new SampleSecurityProvider();
        //    var message = Message.Create(new EhlloMessage {Ehllo = new string('D', 1000)}).As<Message>();
        //    message.SignMessage(securityProvider.CreateOwnedDomainSignature(message));
        //    securityProvider.VerifyOwnedDomainSignature(message);
        //}

        public static void SignFile(byte[] key, String sourceFile, String destFile)
        {
            // Initialize the keyed hash object.
            using (var hmac = new HMACSHA256(key))
            {
                var timer = new Stopwatch();
                timer.Start();
                using (var inStream = new MemoryStream(2 * 1024))
                {
                    for (var i = 0; i < 1000; i++)
                    {
                        var byteArray = Guid.NewGuid().ToByteArray();
                        inStream.Write(byteArray, 0, byteArray.Length);
                    }

                    inStream.Seek(0, SeekOrigin.Begin);
                    var hashValue = hmac.ComputeHash(inStream);
                }
                timer.Stop();
                WriteLine($"Hash computed in {timer.ElapsedMilliseconds} ms");
            }
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