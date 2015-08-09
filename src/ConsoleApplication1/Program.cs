using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using rawf.Framework;
using rawf.Messaging;
using rawf.Sockets;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var server = Task.Factory.StartNew(_ => RunServer(cancellationTokenSource.Token), cancellationTokenSource.Token, TaskCreationOptions.LongRunning);
            Thread.Sleep(TimeSpan.FromSeconds(2));

            RunClient();

            Console.ReadLine();
            cancellationTokenSource.Cancel(true);
        }

        //private static void RunClient()
        //{
        //    var address = new Uri("tcp://127.0.0.1:5000");
        //    var id = new byte[] { 1, 2, 3 };

        //    using (var socketFactory = new SocketFactory())
        //    {
        //        ISocket socket;
        //        using (socket = socketFactory.CreateRouterSocket())
        //        {
        //            socket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
        //            //socket.SetMandatoryRouting();
        //            socket.Connect(address);

        //            var message = (Message)Message.Create(new HelloMessage {Greeting = Guid.NewGuid().ToString()}, HelloMessage.MessageIdentity);
        //            message.SetSocketIdentity(id);
        //            socket.SendMessage(message);

        //            Thread.Sleep(TimeSpan.FromSeconds(2));

        //            socket.Disconnect(address);
        //            socket.Dispose();
        //            Console.WriteLine("Disconnected");

        //            Thread.Sleep(TimeSpan.FromSeconds(5));

        //            socket = socketFactory.CreateRouterSocket();
        //            socket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
        //            socket.Connect(address);
        //            Console.WriteLine("Reconnected");

        //            message = (Message) Message.Create(new HelloMessage { Greeting = Guid.NewGuid().ToString() }, HelloMessage.MessageIdentity);
        //            message.SetSocketIdentity(id);
        //            socket.SendMessage(message);
        //        }
        //    }
        //}

        private static void RunClient()
        {
            var address = new Uri("tcp://127.0.0.1:5000");
            var id = new byte[] { 1, 2, 3 };

            using (var socketFactory = new SocketFactory())
            {
                ISocket socket;
                using (socket = socketFactory.CreateDealerSocket())
                {
                    socket.Connect(address);

                    var message = (Message)Message.Create(new HelloMessage { Greeting = Guid.NewGuid().ToString() }, HelloMessage.MessageIdentity);
                    socket.SendMessage(message);

                    Thread.Sleep(TimeSpan.FromSeconds(2));

                    socket.Disconnect(address);
                    Console.WriteLine("Disconnected");

                    Thread.Sleep(TimeSpan.FromSeconds(5));

                    socket.Connect(address);
                    Console.WriteLine("Reconnected");

                    message = (Message)Message.Create(new HelloMessage { Greeting = Guid.NewGuid().ToString() }, HelloMessage.MessageIdentity);
                    socket.SendMessage(message);
                }
            }
        }

        private static void RunServer(CancellationToken token)
        {
            var id = new byte[] { 1, 2, 3 };
            using (var socketFactory = new SocketFactory())
            {
                using (var socket = socketFactory.CreateRouterSocket())
                {
                    socket.SetMandatoryRouting();
                    //socket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
                    //socket.SetIdentity(id);
                    id = socket.GetIdentity();
                    socket.Bind(new Uri("tcp://127.0.0.1:5000"));
                    while (!token.IsCancellationRequested)
                    {
                        var message  = socket.ReceiveMessage(token);
                        
                        Console.WriteLine(message?.GetPayload<HelloMessage>()?.Greeting);
                    }
                }
            }
        }
    }
}
