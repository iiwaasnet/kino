using System;
using System.Threading;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Consensus;
using kino.Consensus.Configuration;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
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
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly IMessageSerializer serializer;
        private readonly ILogger logger;
        private readonly byte[] leaderPayload;

        public RendezvousService(ILeaseProvider leaseProvider,
                                 ISynodConfiguration synodConfig,
                                 ISocketFactory socketFactory,
                                 IMessageSerializer serializer,
                                 RendezvousConfiguration config,
                                 IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                 ILogger logger)
        {
            this.socketFactory = socketFactory;
            this.logger = logger;
            this.serializer = serializer;
            localNode = synodConfig.LocalNode;
            this.leaseProvider = leaseProvider;
            this.config = config;
            this.performanceCounterManager = performanceCounterManager;
            cancellationTokenSource = new CancellationTokenSource();
            leaderPayload = serializer.Serialize(new RendezvousNode
                                                 {
                                                     MulticastUri = config.BroadcastUri.ToSocketAddress(),
                                                     UnicastUri = config.UnicastUri.ToSocketAddress()
                                                 });
        }

        public bool Start(TimeSpan startTimeout)
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

                return gateway.SignalAndWait(startTimeout, cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            messageProcessing?.Wait();
            pinging?.Wait();
            leaseProvider.Dispose();
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
            return lease != null && Unsafe.ArraysEqual(lease.OwnerIdentity, localNode.SocketIdentity);
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
            socket.ReceiveRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.RendezvousSocketReceiveRate);
            socket.Bind(config.UnicastUri);

            return socket;
        }

        private ISocket CreateBroadcastSocket()
        {
            var socket = socketFactory.CreatePublisherSocket();
            socket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.RendezvousBroadcastSocketSendRate);
            socket.Bind(config.BroadcastUri);

            return socket;
        }

        private ISocket CreatePingNotificationSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.RendezvousPingSocketSendRate);
            socket.Connect(config.UnicastUri);

            return socket;
        }
    }
}