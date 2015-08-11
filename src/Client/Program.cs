using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Client.Messages;
using rawf.Client;
using rawf.Connectivity;
using rawf.Messaging;

namespace Client
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new MainModule());
            var container = builder.Build();

            var ccMon = container.Resolve<IClusterConfigurationMonitor>();
            ccMon.Start();
            var messageRouter = container.Resolve<IMessageRouter>();
            messageRouter.Start();
            var messageHub = container.Resolve<IMessageHub>();
            messageHub.Start();

            Console.WriteLine("Client is running...");

            //var message = Message.CreateFlowStartMessage(new HelloMessage {Greeting = "Hello world!"}, HelloMessage.MessageIdentity);
            //var callback = new CallbackPoint(EhlloMessage.MessageIdentity);
            //var promise = messageHub.EnqueueRequest(message, callback);
            //var resp = promise.GetResponse().Result.GetPayload<EhlloMessage>();
            //Console.WriteLine(resp.Ehllo);

            RunTest(messageHub, 100000);

            Console.ReadLine();
            messageHub.Stop();
            messageRouter.Stop();
            ccMon.Stop();
            container.Dispose();
        }

        private static void RunTest(IMessageHub messageHub, int runs)
        {
            var callbackPoint = new CallbackPoint(EhlloMessage.MessageIdentity);

            var timer = new Stopwatch();
            timer.Start();

            var responses = new List<Task<IMessage>>();
            for (var i = 0; i < runs; i++)
            {
                var message = Message.CreateFlowStartMessage(new HelloMessage { Greeting = "Hello" }, HelloMessage.MessageIdentity);
                //var promise = messageHub.EnqueueRequest(message, callbackPoint);
                //if (promise.GetResponse().Wait(TimeSpan.FromSeconds(1)))
                //{
                //    var msg = promise.GetResponse().Result.GetPayload<EhlloMessage>();
                //    Console.WriteLine($"Received: {msg.Ehllo}");
                //}
                //else
                //{
                //    Console.WriteLine("Timeout....");
                //}
                //Thread.Sleep(TimeSpan.FromSeconds(3));
                responses.Add(messageHub.EnqueueRequest(message, callbackPoint).GetResponse());
            }

            responses.ForEach(r =>
                              {
                                  try
                                  {
                                      //if (r.Wait(TimeSpan.FromSeconds(1)))
                                      //{
                                      var msg = r.Result.GetPayload<EhlloMessage>();
                                      //Console.WriteLine($"Received: {msg.Ehllo}");
                                      //}
                                      //else
                                      //{
                                      //    throw new TimeoutException();
                                      //}
                                  }
                                  catch (Exception err)
                                  {
                                      Console.WriteLine($"Error happened: {err.ToString()}");
                                  }
                              });

            timer.Stop();

            Console.WriteLine($"Done {runs} times in {timer.ElapsedMilliseconds} msec");
        }
    }
}