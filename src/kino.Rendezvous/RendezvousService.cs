using System;
using System.Threading;
using System.Threading.Tasks;
using kino.Consensus;
using kino.Consensus.Configuration;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Sockets;
using kino.Rendezvous.Configuration;

namespace kino.Rendezvous
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
        private readonly IMessageSerializer serializer;
        private readonly ILogger logger;
        private readonly byte[] leaderPayload;

        public RendezvousService(ILeaseProvider leaseProvider,
                                 ISynodConfiguration synodConfig,
                                 ISocketFactory socketFactory,
                                 IMessageSerializer serializer,
                                 RendezvousConfiguration config,
                                 ILogger logger)
        {
            this.socketFactory = socketFactory;
            this.logger = logger;
            this.serializer = serializer;
            localNode = synodConfig.LocalNode;
            this.leaseProvider = leaseProvider;
            this.config = config;
            cancellationTokenSource = new CancellationTokenSource();
            leaderPayload = serializer.Serialize(new RendezvousNode
                                                 {
                                                     MulticastUri = config.MulticastUri.ToSocketAddress(),
                                                     UnicastUri = config.UnicastUri.ToSocketAddress()
                                                 });
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
                        var pingId = 0UL;

                        while (!token.IsCancellationRequested)
                        {
                            try
                            {
                                var message = NodeIsLeader()
                                                  ? CreatePing(ref pingId)
                                                  : CreateNotLeaderMessage();
                                if (message != null)
                                {
                                    pingNotificationSocket.SendMessage(message);
                                }
                                wait.Wait(config.PingInterval, token);
                            }
                            catch (OperationCanceledException)
                            {
                            }
                            catch (Exception err)
                            {
                                logger.Error(err);
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

        private bool NodeIsLeader()
        {
            var lease = leaseProvider.GetLease(leaderPayload);
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
                            try
                            {
                                var message = unicastSocket.ReceiveMessage(token);

                                message = (NodeIsLeader()) ? message : CreateNotLeaderMessage();

                                if (message != null)
                                {
                                    broadcastSocket.SendMessage(message);
                                }
                            }
                            catch (Exception err)
                            {
                                logger.Error(err);
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

        private IMessage CreatePing(ref ulong pingId)
        {
            return Message.Create(new PingMessage
                                  {
                                      PingId = pingId++,
                                      PingInterval = config.PingInterval
                                  });
        }

        private IMessage CreateNotLeaderMessage()
        {
            var lease = leaseProvider.GetLease(leaderPayload);
            if (lease != null)
            {
                return Message.Create(new RendezvousNotLeaderMessage
                                      {
                                          NewLeader = serializer.Deserialize<RendezvousNode>(lease.OwnerPayload)
                                      });
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
        }
    }
}