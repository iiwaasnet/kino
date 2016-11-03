using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using C5;
using kino.Configuration;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Cluster
{
    public class ClusterHealthMonitor : IClusterHealthMonitor
    {
        private readonly ISocketFactory socketFactory;
        private readonly ClusterHealthMonitorConfiguration config;
        private readonly ILocalSendingSocket<IMessage> routerLocalSocket;
        private readonly ILogger logger;
        private CancellationTokenSource cancellationTokenSource;
        private Task monitoring;
        private readonly ILocalSocket<IMessage> multiplexingSocket;
        private readonly IDictionary<SocketIdentifier, ClusterMemberMeta> peers;
        private Task messageProcessing;
        private TimeSpan deadPeersCheckInterval;
        private IDisposable observer;

        public ClusterHealthMonitor(ISocketFactory socketFactory,
                                    ILocalSocketFactory localSocketFactory,
                                    ClusterHealthMonitorConfiguration config,
                                    ILocalSendingSocket<IMessage> routerLocalSocket,
                                    ILogger logger)
        {
            deadPeersCheckInterval = TimeSpan.FromDays(1);
            this.socketFactory = socketFactory;
            peers = new HashDictionary<SocketIdentifier, ClusterMemberMeta>();
            multiplexingSocket = localSocketFactory.Create<IMessage>();
            this.config = config;
            this.routerLocalSocket = routerLocalSocket;
            this.logger = logger;
        }

        public void StartPeerMonitoring(SocketIdentifier socketIdentifier, Health health)
            => multiplexingSocket.Send(Message.Create(new StartPeerMonitoringMessage
                                                      {
                                                          SocketIdentity = socketIdentifier.Identity,
                                                          Health = new Messaging.Messages.Health
                                                                   {
                                                                       Uri = health.Uri,
                                                                       HeartBeatInterval = health.HeartBeatInterval
                                                                   }
                                                      }));

        public void DeletePeer(SocketIdentifier socketIdentifier)
            => multiplexingSocket.Send(Message.Create(new DeletePeerMessage {SocketIdentity = socketIdentifier.Identity}));

        public void Start()
        {
            cancellationTokenSource = new CancellationTokenSource();

            monitoring = Task.Factory.StartNew(_ => MonitorPeers(cancellationTokenSource.Token), TaskCreationOptions.LongRunning, cancellationTokenSource.Token);
            messageProcessing = Task.Factory.StartNew(_ => ProcessMessages(cancellationTokenSource.Token), TaskCreationOptions.LongRunning, cancellationTokenSource.Token);
        }

        public void Stop()
        {
            cancellationTokenSource?.Cancel();
            monitoring?.Wait();
            messageProcessing?.Wait();
            cancellationTokenSource?.Dispose();
        }

        private void ProcessMessages(CancellationToken token)
        {
            try
            {
                var waitHandles = new[]
                                  {
                                      multiplexingSocket.CanReceive(),
                                      token.WaitHandle
                                  };
                using (var publisherSocket = CreatePublisherSocket())
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            if (WaitHandle.WaitAny(waitHandles) == 0)
                            {
                                var message = multiplexingSocket.TryReceive();
                                if (message != null)
                                {
                                    publisherSocket.SendMessage(message);
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
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }

            logger.Warn($" {GetType().Name} message processing stopped. ");
        }

        private void ProcessMessage(IMessage message, ISocket socket)
        {
            var _ = ProcessHeartBeatMessage(message)
                    || ProcessStartPeerMonitoringMessage(message, socket)
                    || ProcessCheckDeadPeersMessage(message)
                    || ProcessDeletePeerMessage(message, socket);
        }

        private bool ProcessDeletePeerMessage(IMessage message, ISocket socket)
        {
            var shouldHandle = IsDeletePeerMessage(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<DeletePeerMessage>();
                ClusterMemberMeta meta;
                var socketIdentifier = new SocketIdentifier(payload.SocketIdentity);
                if (peers.Find(ref socketIdentifier, out meta))
                {
                    peers.Remove(socketIdentifier);

                    logger.Debug($"Left {peers.Count} to monitor.");

                    socket.Disconnect(new Uri(meta.HealthUri));
                }
                else
                {
                    logger.Warn($"Unable to disconnect from unknown peer: SocketIdentity [{payload.SocketIdentity.GetAnyString()}]");
                }
            }

            return shouldHandle;
        }

        private bool ProcessCheckDeadPeersMessage(IMessage message)
        {
            var shouldHandle = IsCheckDeadPeersMessage(message);
            if (shouldHandle)
            {
                var now = DateTime.UtcNow;
                var deadNodes = peers.Where(p => HeartBeatExpired(now, p))
                                     .ToList();
                foreach (var deadNode in deadNodes)
                {
                    routerLocalSocket.Send(Message.Create(new UnregisterUnreachableNodeMessage {SocketIdentity = deadNode.Key.Identity}));
                    logger.Debug($"Unreachable node {deadNode.Key.Identity.GetAnyString()}@{deadNode.Value.HealthUri} detected. Route deletion scheduled.");
                }
            }

            return shouldHandle;
        }

        private bool HeartBeatExpired(DateTime now, KeyValuePair<SocketIdentifier, ClusterMemberMeta> p)
            => now - p.Value.LastKnownHeartBeat
               > p.Value.HeartBeatInterval.MultiplyBy(config.MissingHeartBeatsBeforeDeletion);

        private bool ProcessStartPeerMonitoringMessage(IMessage message, ISocket socket)
        {
            var shouldHandle = IsStartPeerMonitoringMessage(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<StartPeerMonitoringMessage>();

                logger.Debug($"Received {typeof(StartPeerMonitoringMessage).Name} for node {payload.SocketIdentity.GetAnyString()}");

                var meta = new ClusterMemberMeta
                           {
                               HealthUri = payload.Health.Uri,
                               HeartBeatInterval = payload.Health.HeartBeatInterval,
                               LastKnownHeartBeat = DateTime.UtcNow
                           };
                peers.FindOrAdd(new SocketIdentifier(payload.SocketIdentity), ref meta);
                StartDeadPeersCheck(meta.HeartBeatInterval);
                socket.Connect(new Uri(meta.HealthUri));

                logger.Debug($"Connected to peer at {meta.HealthUri} for HeartBeat monitoring.");
            }

            return shouldHandle;
        }

        private void StartDeadPeersCheck(TimeSpan newHeartBeatInterval)
        {
            if (newHeartBeatInterval < deadPeersCheckInterval)
            {
                deadPeersCheckInterval = newHeartBeatInterval;
                observer?.Dispose();
                observer = Observable.Interval(deadPeersCheckInterval).Subscribe(_ => CheckDeadPeers());
            }
        }

        private void CheckDeadPeers()
            => multiplexingSocket.Send(Message.Create(new CheckDeadPeersMessage()));

        private bool ProcessHeartBeatMessage(IMessage message)
        {
            var shouldHandle = IsHeartBeatMessage(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<HeartBeatMessage>();
                var socketIdentifier = new SocketIdentifier(payload.SocketIdentity);
                ClusterMemberMeta meta;
                if (peers.Find(ref socketIdentifier, out meta))
                {
                    meta.LastKnownHeartBeat = DateTime.UtcNow;
                    //logger.Debug($"Received HeartBeat from node {socketIdentifier}");
                }
                else
                {
                    //TODO: Send DicoveryMessage? What if peer is not supporting message Domains to be used by this node?
                    logger.Warn($"HeartBeat came from unknown peer: SocketIdentity [{payload.SocketIdentity.GetAnyString()}]");
                }
            }

            return shouldHandle;
        }

        private void MonitorPeers(CancellationToken token)
        {
            try
            {
                using (var socket = CreateSubscriberSocket())
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var message = socket.ReceiveMessage(token);
                            if (message != null)
                            {
                                logger.Debug($"{GetType().Name} received {message.Identity.GetAnyString()} message");
                                ProcessMessage(message, socket);
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
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }

            logger.Warn($"{GetType().Name} stopped.");
        }

        private ISocket CreateSubscriberSocket()
        {
            var socket = socketFactory.CreateSubscriberSocket();
            socket.Connect(config.IntercomEndpoint);
            socket.Subscribe();

            return socket;
        }

        private ISocket CreatePublisherSocket()
        {
            var socket = socketFactory.CreatePublisherSocket();
            socket.Bind(config.IntercomEndpoint);

            return socket;
        }

        private static bool IsHeartBeatMessage(IMessage message)
            => message.Equals(KinoMessages.HeartBeat);

        private static bool IsCheckDeadPeersMessage(IMessage message)
            => message.Equals(KinoMessages.CheckDeadPeers);

        private static bool IsStartPeerMonitoringMessage(IMessage message)
            => message.Equals(KinoMessages.StartPeerMonitoring);

        private static bool IsDeletePeerMessage(IMessage message)
            => message.Equals(KinoMessages.DeletePeer);
    }
}