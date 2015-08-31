using System;
using System.Threading;
using System.Threading.Tasks;
using rawf.Diagnostics;
using rawf.Framework;
using rawf.Messaging;
using rawf.Messaging.Messages;
using rawf.Rendezvous.Consensus;
using rawf.Sockets;

namespace rawf.Rendezvous
{
    public class RendezvousService : IRendezvousService
    {
        private readonly ISocketFactory socketFactory;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly ILeaseProvider leaseProvider;
        private readonly Node localNode;
        private Task messageProcessing;
        private Task pinging;
        private readonly RendezvousConfiguration config;
        private readonly ILogger logger;

        public RendezvousService(ILeaseProvider leaseProvider,
                                 ISynodConfiguration synodConfig,
                                 ISocketFactory socketFactory,
                                 RendezvousConfiguration config,
                                 ILogger logger)
        {
            this.socketFactory = socketFactory;
            this.logger = logger;
            localNode = synodConfig.LocalNode;
            this.leaseProvider = leaseProvider;
            this.config = config;
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
                            var message = NodeIsLeader()
                                              ? Message.Create(new PingMessage(), PingMessage.MessageIdentity)
                                              : CreateNotLeaderMessage();
                            if (message != null)
                            {
                                pingNotificationSocket.SendMessage(message);
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
                logger.Error(err);
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

                            message = (NodeIsLeader()) ? message : CreateNotLeaderMessage();

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
                logger.Error(err);
            }
        }

        private IMessage CreateNotLeaderMessage()
        {
            var lease = leaseProvider.GetLease();
            if (lease != null)
            {
                return Message.Create(new RendezvousNotLeaderMessage
                                      {
                                          LeaderMulticastUri = lease.OwnerEndpoint.MulticastUri.ToSocketAddress(),
                                          LeaderUnicastUri = lease.OwnerEndpoint.UnicastUri.ToSocketAddress()
                                      },
                                      RendezvousNotLeaderMessage.MessageIdentity);
            }

            return null;
        }

        private ISocket CreateUnicastSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            socket.SetMandatoryRouting();
            socket.Bind(config.UnicastUri);

            return socket;
        }

        private ISocket CreateBroadcastSocket()
        {
            var socket = socketFactory.CreatePublisherSocket();
            socket.Bind(config.MulticastUri);

            return socket;
        }

        private ISocket CreatePingNotificationSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(config.UnicastUri);

            return socket;
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            messageProcessing.Wait();
            pinging.Wait();
            leaseProvider.Dispose();
        }
    }
}