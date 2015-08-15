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

            var messageRouter = container.Resolve<IMessageRouter>();
            messageRouter.Start();
            Thread.Sleep(TimeSpan.FromMilliseconds(30));

            var ccMon = container.Resolve<IClusterConfigurationMonitor>();
            ccMon.Start();
            var messageHub = container.Resolve<IMessageHub>();
            messageHub.Start();

            Thread.Sleep(TimeSpan.FromSeconds(2));
            Console.WriteLine($"Client is running... {DateTime.Now}");

            RunTest(messageHub, 1000);

            Console.ReadLine();
            messageHub.Stop();
            messageRouter.Stop();
            ccMon.Stop();
            container.Dispose();
        }

        private static void RunTest(IMessageHub messageHub, int runs)
        {
            var callbackPoint = new CallbackPoint(EhlloMessage.MessageIdentity);
            var rnd = new Random((int) DateTime.UtcNow.Ticks & 0x0000ffff);

            var timer = new Stopwatch();
            timer.Start();

            var responseWaitTimeout = TimeSpan.FromMilliseconds(60000 + rnd.Next(0, 100));
            var responses = new List<Task<IMessage>>();
            for (var i = 0; i < runs; i++)
            {
                try
                {
                    var message = Message.CreateFlowStartMessage(new HelloMessage { Greeting = "Hello" }, HelloMessage.MessageIdentity);
                    var promise = messageHub.EnqueueRequest(message, callbackPoint);
                    if (promise.GetResponse().Wait(responseWaitTimeout))
                    {
                        var msg = promise.GetResponse().Result.GetPayload<EhlloMessage>();
                        Console.WriteLine($"Received: {msg.Ehllo}");
                    }
                    else
                    {
                        Console.WriteLine("Timeout....");
                    }
                    //responses.Add(messageHub.EnqueueRequest(message, callbackPoint, responseWaitTimeout).GetResponse());
                }
                catch (Exception err)
                {
                    Console.WriteLine(err);
                }
                
            }

            //responses.ForEach(r =>
            //                  {
            //                      try
            //                      {
            //                          if (r.Wait(responseWaitTimeout))
            //                          {
            //                              var msg = r.Result.GetPayload<EhlloMessage>();
            //                              //Console.WriteLine($"Received: {msg.Ehllo}");
            //                          }
            //                          else
            //                          {
            //                              throw new TimeoutException();
            //                          }
            //                      }
            //                      catch (Exception err)
            //                      {
            //                          Console.WriteLine($"Error happened: {err.ToString()} {DateTime.Now}");
            //                      }
            //                  });

            timer.Stop();

            Console.WriteLine($"Done {runs} times in {timer.ElapsedMilliseconds} msec");
        }
    }
}