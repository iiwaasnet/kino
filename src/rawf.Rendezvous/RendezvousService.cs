using System;
using System.Threading;
using System.Threading.Tasks;
using rawf.Messaging;
using rawf.Messaging.Messages;
using rawf.Sockets;
using TypedConfigProvider;

namespace rawf.Rendezvous
{
    public class RendezvousService : IRendezvousService
    {
        private readonly ISocketFactory socketFactory;
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task messageProcessing;
        private Task pinging;
        private readonly ApplicationConfiguration config;

        public RendezvousService(ISocketFactory socketFactory, IConfigProvider configProvider)
        {
            this.socketFactory = socketFactory;
            config = configProvider.GetConfiguration<ApplicationConfiguration>();
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            const int participantCount = 3;
            using (var gateway = new Barrier(participantCount))
            {
                messageProcessing = Task.Factory.StartNew(_ => ProcessMessages(cancellationTokenSource.Token, gateway),
                                                          TaskCreationOptions.LongRunning);
                pinging = Task.Factory.StartNew(_ => PingClusterMembers(cancellationTokenSource.Token, gateway),
                                                          TaskCreationOptions.LongRunning);

                gateway.SignalAndWait(cancellationTokenSource.Token);
            }
        }

        private void PingClusterMembers(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var wait = new ManualResetEventSlim(false))
                {
                    using (var pingNotificationSocket = CreatePingNotificationSocket())
                    {
                        gateway.SignalAndWait(token);

                        while (!token.IsCancellationRequested)
                        {
                            var message = Message.Create(new PingMessage(), PingMessage.MessageIdentity);
                            pingNotificationSocket.SendMessage(message);

                            wait.Wait(config.PingInterval, token);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }

        

        private void ProcessMessages(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var broadcastSocket = CreateBroadcastSocket())
                {
                    using (var unicastSocket = CreateUnicastSocket())
                    {
                        gateway.SignalAndWait(token);
                        while (!token.IsCancellationRequested)
                        {
                            var message = unicastSocket.ReceiveMessage(token);
                            if (message != null)
                            {
                                broadcastSocket.SendMessage(message);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }

        private ISocket CreateUnicastSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            socket.Bind(new Uri(config.UnicastUri));

            return socket;
        }

        private ISocket CreateBroadcastSocket()
        {
            var socket = socketFactory.CreatePublisherSocket();
            socket.Bind(new Uri(config.BroadcastUri));

            return socket;
        }

        private ISocket CreatePingNotificationSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(new Uri(config.UnicastUri));

            return socket;
        }
        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            messageProcessing.Wait();
        }
    }
}