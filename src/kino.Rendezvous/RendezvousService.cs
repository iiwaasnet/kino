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
        private Task unicastMessageReceiving;
        private readonly IRendezvousConfigurationProvider configProvider;
        private readonly IPartnerNetworkConnectorManager partnerNetworkConnectorManager;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly IMessageSerializer serializer;
        private readonly ILogger logger;
        private readonly byte[] leaderPayload;
        private readonly IMessage pongMessage;
        private readonly ILocalReceivingSocket<IMessage> partnerClusterSocket;
        private readonly ILocalSocket<IMessage> unicastForwardingSocket;

        public RendezvousService(ILeaseProvider leaseProvider,
                                 ISynodConfigurationProvider synodConfigProvider,
                                 ISocketFactory socketFactory,
                                 IMessageSerializer serializer,
                                 ILocalSocketFactory localSocketFactory,
                                 IRendezvousConfigurationProvider configProvider,
                                 IPartnerNetworkConnectorManager partnerNetworkConnectorManager,
                                 IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                 ILogger logger)
        {
            this.socketFactory = socketFactory;
            this.logger = logger;
            this.serializer = serializer;
            localNode = synodConfigProvider.LocalNode;
            this.leaseProvider = leaseProvider;
            this.configProvider = configProvider;
            this.partnerNetworkConnectorManager = partnerNetworkConnectorManager;
            this.performanceCounterManager = performanceCounterManager;
            cancellationTokenSource = new CancellationTokenSource();
            pongMessage = Message.Create(new PongMessage());
            leaderPayload = serializer.Serialize(new RendezvousNode
                                                 {
                                                     BroadcastUri = configProvider.BroadcastUri,
                                                     UnicastUri = configProvider.UnicastUri
                                                 });
            partnerClusterSocket = localSocketFactory.CreateNamed<IMessage>(NamedSockets.PartnerClusterSocket);
            unicastForwardingSocket = localSocketFactory.Create<IMessage>();
        }

        public bool Start(TimeSpan startTimeout)
        {
            const int participantCount = 4;
            using (var gateway = new Barrier(participantCount))
            {
                messageProcessing = Task.Factory.StartNew(_ => ProcessMessages(cancellationTokenSource.Token, gateway),
                                                          cancellationTokenSource.Token,
                                                          TaskCreationOptions.LongRunning);
                unicastMessageReceiving = Task.Factory.StartNew(_ => ReceiveUnicastMessages(cancellationTokenSource.Token, gateway),
                                                                cancellationTokenSource.Token,
                                                                TaskCreationOptions.LongRunning);
                heartBeating = Task.Factory.StartNew(_ => SendHeartBeat(cancellationTokenSource.Token, gateway),
                                                     cancellationTokenSource.Token,
                                                     TaskCreationOptions.LongRunning);
                partnerNetworkConnectorManager.StartConnectors();
                return gateway.SignalAndWait(startTimeout, cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            partnerNetworkConnectorManager.StopConnectors();
            cancellationTokenSource.Cancel();
            messageProcessing?.Wait();
            unicastMessageReceiving?.Wait();
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
                                    heartBeatSocket.Send(message);
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

        private void ReceiveUnicastMessages(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var unicastSocket = CreateUnicastSocket())
                {
                    gateway.SignalAndWait(token);
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var message = unicastSocket.Receive(token);
                            if (message != null)
                            {
                                unicastForwardingSocket.Send(message);
                            }
                        }
                        catch (Exception err)
                        {
                            logger.Error(err);
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

        private void ProcessMessages(CancellationToken token, Barrier gateway)
        {
            const int cancellationToken = 2;
            try
            {
                var waitHandles = new[]
                                  {
                                      unicastForwardingSocket.CanReceive(),
                                      partnerClusterSocket.CanReceive(),
                                      token.WaitHandle
                                  };

                using (var partnerBroadcastSocket = CreatePartnerBroadcastSocket())
                {
                    using (var broadcastSocket = CreateBroadcastSocket())
                    {
                        gateway.SignalAndWait(token);
                        while (!token.IsCancellationRequested)
                        {
                            try
                            {
                                var receiverId = WaitHandle.WaitAny(waitHandles);
                                if (receiverId != cancellationToken)
                                {
                                    TryProcessMessage(unicastForwardingSocket, broadcastSocket, partnerBroadcastSocket);
                                    TryProcessMessage(partnerClusterSocket, broadcastSocket);
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

            void TryProcessMessage(ILocalReceivingSocket<IMessage> receivingSocket, params ISocket[] sockets)
            {
                var message = receivingSocket.TryReceive();
                if (message != null)
                {
                    message = NodeIsLeader()
                                  ? ProcessMessage(message)
                                  : CreateNotLeaderMessage();

                    foreach (var sendingSocket in sockets)
                    {
                        SyntaxSugar.SafeExecute(() => sendingSocket.Send(message), logger);
                    }
                }
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

        private ISocket CreatePartnerBroadcastSocket()
        {
            var socket = socketFactory.CreatePublisherSocket();
            socket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.RendezvousBroadcastSocketSendRate);
            socket.Bind(configProvider.PartnerBroadcastUri);

            logger.Info($"Broadcasting cluster messages started on: {configProvider.PartnerBroadcastUri}");

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