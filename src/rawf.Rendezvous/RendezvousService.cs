using System;
using System.Threading;
using System.Threading.Tasks;
using rawf.Consensus;
using rawf.Framework;
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
        private readonly ILeaseProvider leaseProvider;
        private readonly INode localNode;
        private Task messageProcessing;
        private Task pinging;
        private readonly ApplicationConfiguration config;

        public RendezvousService(ILeaseProvider leaseProvider,
                                 ISynodConfiguration synodConfig,
                                 ISocketFactory socketFactory,
                                 IConfigProvider configProvider)
        {
            this.socketFactory = socketFactory;
            localNode = synodConfig.LocalNode;
            this.leaseProvider = leaseProvider;
            config = configProvider.GetConfiguration<ApplicationConfiguration>();
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            const int participantCount = 3;
            using (var gateway = new Barrier(participantCount))
            {
                messageProcessing = Task.Factory.StartNew(_ => ProcessMessages(cancellationTokenSource.Token, gateway),
                                                          cancellationTokenSource.Token,
                                                          TaskCreationOptions.LongRunning);
                pinging = Task.Factory.StartNew(_ => PingClusterMembers(cancellationTokenSource.Token, gateway),
                                                cancellationTokenSource.Token,
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
                            if (NodeIsLeader())
                            {
                                var message = Message.Create(new PingMessage(), PingMessage.MessageIdentity);
                                pingNotificationSocket.SendMessage(message);

                                Console.WriteLine("Ping");
                            }
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

        private bool NodeIsLeader()
        {
            var lease = leaseProvider.GetLease();
            return lease != null && Unsafe.Equals(lease.OwnerIdentity, localNode.SocketIdentity);
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
                                if (NodeIsLeader())
                                {
                                    broadcastSocket.SendMessage(message);
                                }
                                //else
                                //{
                                //    unicastSocket.SendMessage();
                                //}
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
            socket.SetMandatoryRouting();
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
            pinging.Wait();
            leaseProvider.Dispose();
        }
    }
}