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

            RunBroadcastTest(messageHub, 100);

            ReadLine();
            messageHub.Stop();
            messageRouter.Stop();
            ccMon.Stop();
            container.Dispose();
        }

        private static void RunTest(IMessageHub messageHub, int runs)
        {
            var callbackPoint = new CallbackPoint(GroupCharsResponseMessage.MessageIdentity);
            var rnd = new Random((int) DateTime.UtcNow.Ticks & 0x0000ffff);

            var timer = new Stopwatch();
            timer.Start();

            var responseWaitTimeout = TimeSpan.FromMilliseconds(60000 + rnd.Next(0, 100));
            var responses = new List<Task<IMessage>>();
            for (var i = 0; i < runs; i++)
            {
                try
                {
                    var message = Message.CreateFlowStartMessage(new HelloMessage { Greeting = Guid.NewGuid().ToString() }, HelloMessage.MessageIdentity);
                    var promise = messageHub.EnqueueRequest(message, callbackPoint);
                    if (promise.GetResponse().Wait(responseWaitTimeout))
                    {
                        var msg = promise.GetResponse().Result.GetPayload<GroupCharsResponseMessage>();
                        WriteLine($"Text: {msg.Text}");
                      foreach (var groupInfo in msg.Groups)
                      {
                        WriteLine($"Char: {groupInfo.Char} - {groupInfo.Count} times");
                      }
                    }
                    else
                    {
                        WriteLine("Timeout....");
                    }
                    //responses.Add(messageHub.EnqueueRequest(message, callbackPoint, responseWaitTimeout).GetResponse());
                }
                catch (Exception err)
                {
                    WriteLine(err);
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
            //                          WriteLine($"Error happened: {err.ToString()} {DateTime.Now}");
            //                      }
            //                  });

            timer.Stop();

            WriteLine($"Done {runs} times in {timer.ElapsedMilliseconds} msec");
        }

        private static void RunBroadcastTest(IMessageHub messageHub, int runs)
        {
            var timer = new Stopwatch();
            timer.Start();

            for (var i = 0; i < runs; i++)
            {
                try
                {
                    var message = Message.Create(new HelloMessage { Greeting = Guid.NewGuid().ToString() }, HelloMessage.MessageIdentity, DistributionPattern.Broadcast);
                    messageHub.SendOneWay(message);
                }
                catch (Exception err)
                {
                    WriteLine(err);
                }
                
            }
            timer.Stop();

            WriteLine($"Done {runs} times in {timer.ElapsedMilliseconds} msec");
        }
    }
}