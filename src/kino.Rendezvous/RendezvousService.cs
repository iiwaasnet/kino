using System;
using System.Threading;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Consensus;
using kino.Consensus.Configuration;
using kino.Core;
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
        private Task heartBeating;
        private readonly IRendezvousConfigurationProvider configProvider;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly IMessageSerializer serializer;
        private readonly ILogger logger;
        private readonly byte[] leaderPayload;
        private readonly IMessage pongMessage;

        public RendezvousService(ILeaseProvider leaseProvider,
                                 ISynodConfigurationProvider synodConfigProvider,
                                 ISocketFactory socketFactory,
                                 IMessageSerializer serializer,
                                 IRendezvousConfigurationProvider configProvider,
                                 IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                 ILogger logger)
        {
            this.socketFactory = socketFactory;
            this.logger = logger;
            this.serializer = serializer;
            localNode = synodConfigProvider.LocalNode;
            this.leaseProvider = leaseProvider;
            this.configProvider = configProvider;
            this.performanceCounterManager = performanceCounterManager;
            cancellationTokenSource = new CancellationTokenSource();
            pongMessage = Message.Create(new PongMessage());
            leaderPayload = serializer.Serialize(new RendezvousNode
                                                 {
                                                     BroadcastUri = configProvider.BroadcastUri,
                                                     UnicastUri = configProvider.UnicastUri
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
                heartBeating = Task.Factory.StartNew(_ => SendHeartBeat(cancellationTokenSource.Token, gateway),
                                                     cancellationTokenSource.Token,
                                                     TaskCreationOptions.LongRunning);

                return gateway.SignalAndWait(startTimeout, cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            messageProcessing?.Wait();
            heartBeating?.Wait();
            leaseProvider.Dispose();
        }

        public bool IsConsensusReached()
            => leaseProvider.GetLease(leaderPayload) != null;

        private void SendHeartBeat(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var wait = new ManualResetEventSlim(false))
                {
                    using (var heartBeatSocket = CreateHeartBeatSocket())
                    {
                        gateway.SignalAndWait(token);

                        while (!token.IsCancellationRequested)
                        {
                            try
                            {
                                var message = NodeIsLeader()
                                                  ? CreateHeartBeat()
                                                  : CreateNotLeaderMessage();
                                if (message != null)
                                {
                                    heartBeatSocket.SendMessage(message);
                                }
                                wait.Wait(configProvider.HeartBeatInterval, token);
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

                                message = NodeIsLeader()
                                              ? ProcessMessage(message)
                                              : CreateNotLeaderMessage();

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

        private IMessage ProcessMessage(IMessage message)
            => message?.Equals(KinoMessages.Ping) == true
                   ? pongMessage
                   : message;

        private IMessage CreateHeartBeat()
            => Message.Create(new HeartBeatMessage {HeartBeatInterval = configProvider.HeartBeatInterval});

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
            socket.Bind(configProvider.UnicastUri);

            logger.Info($"Receiving cluster messages started on: {configProvider.UnicastUri}");

            return socket;
        }

        private ISocket CreateBroadcastSocket()
        {
            var socket = socketFactory.CreatePublisherSocket();
            socket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.RendezvousBroadcastSocketSendRate);
            socket.Bind(configProvider.BroadcastUri);

            logger.Info($"Broadcasting cluster messages started on: {configProvider.BroadcastUri}");

            return socket;
        }

        private ISocket CreateHeartBeatSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.RendezvousHeartBeatSocketSendRate);
            socket.Connect(configProvider.UnicastUri, true);

            logger.Info($"HeartBeating started on: {configProvider.UnicastUri}");

            return socket;
        }
    }
}