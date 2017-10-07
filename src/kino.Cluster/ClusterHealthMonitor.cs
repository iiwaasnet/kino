using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;
using Bcl = System.Collections.Generic;

namespace kino.Cluster
{
    public class ClusterHealthMonitor : IClusterHealthMonitor
    {
        private readonly ISocketFactory socketFactory;
        private readonly ISecurityProvider securityProvider;
        private readonly ClusterHealthMonitorConfiguration config;
        private readonly ILocalSendingSocket<IMessage> routerLocalSocket;
        private readonly IConnectedPeerRegistry connectedPeerRegistry;
        private readonly ILogger logger;
        private CancellationTokenSource cancellationTokenSource;
        private Task processingMessages;
        private readonly ILocalSocket<IMessage> multiplexingSocket;
        private Task receivingMessages;
        private TimeSpan deadPeersCheckInterval;
        private IDisposable deadPeersCheckObserver;
        private IDisposable stalePeersCheckObserver;

        public ClusterHealthMonitor(ISocketFactory socketFactory,
                                    ILocalSocketFactory localSocketFactory,
                                    ISecurityProvider securityProvider,
                                    ILocalSendingSocket<IMessage> routerLocalSocket,
                                    IConnectedPeerRegistry connectedPeerRegistry,
                                    ClusterHealthMonitorConfiguration config,
                                    ILogger logger)
        {
            deadPeersCheckInterval = TimeSpan.FromDays(1);
            this.socketFactory = socketFactory;
            this.securityProvider = securityProvider;
            multiplexingSocket = localSocketFactory.Create<IMessage>();
            this.config = config;
            this.routerLocalSocket = routerLocalSocket;
            this.connectedPeerRegistry = connectedPeerRegistry;
            this.logger = logger;
        }

        public void StartPeerMonitoring(Node peer, Health health)
            => multiplexingSocket.Send(Message.Create(new StartPeerMonitoringMessage
                                                      {
                                                          SocketIdentity = peer.SocketIdentity,
                                                          Uri = peer.Uri.ToSocketAddress(),
                                                          Health = new Messaging.Messages.Health
                                                                   {
                                                                       Uri = health.Uri,
                                                                       HeartBeatInterval = health.HeartBeatInterval
                                                                   }
                                                      }));

        public void AddPeer(Node peer, Health health)
        {
            logger.Debug($"AddPeer {peer.SocketIdentity.GetAnyString()}@{peer.Uri.ToSocketAddress()}");

            multiplexingSocket.Send(Message.Create(new AddPeerMessage
                                                   {
                                                       SocketIdentity = peer.SocketIdentity,
                                                       Uri = peer.Uri.ToSocketAddress(),
                                                       Health = new Messaging.Messages.Health
                                                                {
                                                                    Uri = health.Uri,
                                                                    HeartBeatInterval = health.HeartBeatInterval
                                                                }
                                                   }));
        }

        public void DeletePeer(ReceiverIdentifier nodeIdentifier)
            => multiplexingSocket.Send(Message.Create(new DeletePeerMessage {NodeIdentity = nodeIdentifier.Identity}));

        public void Start()
        {
            stalePeersCheckObserver = Observable.Interval(config.StalePeersCheckInterval)
                                                .Subscribe(_ => CheckStalePeers());
            cancellationTokenSource = new CancellationTokenSource();
            var participantsCount = 3;
            using (var barrier = new Barrier(participantsCount))
            {
                receivingMessages = Task.Factory.StartNew(_ => ReceiveMessages(cancellationTokenSource.Token, barrier), TaskCreationOptions.LongRunning, cancellationTokenSource.Token);
                processingMessages = Task.Factory.StartNew(_ => ProcessMessages(cancellationTokenSource.Token, barrier), TaskCreationOptions.LongRunning, cancellationTokenSource.Token);
                //TODO: Check if better implementation is possible
                barrier.SignalAndWait(cancellationTokenSource.Token);
                barrier.SignalAndWait(cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            stalePeersCheckObserver?.Dispose();
            deadPeersCheckObserver?.Dispose();
            cancellationTokenSource?.Cancel();
            processingMessages?.Wait();
            receivingMessages?.Wait();
            cancellationTokenSource?.Dispose();
        }

        private void ReceiveMessages(CancellationToken token, Barrier barrier)
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
                    barrier.SignalAndWait(token);
                    barrier.SignalAndWait(token);
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

            logger.Warn($"{GetType().Name} message processing stopped.");
        }

        public void ScheduleConnectivityCheck(ReceiverIdentifier nodeIdentifier)
            => multiplexingSocket.Send(Message.Create(new CheckPeerConnectionMessage
                                                      {
                                                          SocketIdentity = nodeIdentifier.Identity
                                                      }));

        private void ProcessMessages(CancellationToken token, Barrier barrier)
        {
            try
            {
                barrier.SignalAndWait(token);
                using (var socket = CreateSubscriberSocket())
                {
                    barrier.SignalAndWait(token);
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var message = socket.ReceiveMessage(token);
                            if (message != null)
                            {
                                //logger.Debug($"{GetType().Name} received {message.Identity.GetAnyString()} message");
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

        private void ProcessMessage(IMessage message, ISocket socket)
        {
            var _ = ProcessHeartBeatMessage(message, socket)
                    || ProcessStartPeerMonitoringMessage(message, socket)
                    || ProcessAddPeerMessage(message, socket)
                    || ProcessCheckDeadPeersMessage(message)
                    || ProcessCheckStalePeersMessage(message)
                    || ProcessDeletePeerMessage(message, socket)
                    || ProcessCheckPeerConnectionMessage(message);
        }

        private bool ProcessDeletePeerMessage(IMessage message, ISocket socket)
        {
            var shouldHandle = message.Equals(KinoMessages.DeletePeer);
            if (shouldHandle)
            {
                var payload = message.GetPayload<DeletePeerMessage>();
                var socketIdentifier = new ReceiverIdentifier(payload.NodeIdentity);
                var meta = connectedPeerRegistry.Find(socketIdentifier);
                if (meta != null)
                {
                    connectedPeerRegistry.Remove(socketIdentifier);

                    logger.Debug($"Left {connectedPeerRegistry.Count()} nodes to monitor.");
                    if (meta.ConnectionEstablished)
                    {
                        socket.Disconnect(new Uri(meta.HealthUri));
                        logger.Warn($"Stopped HeartBeat monitoring node {payload.NodeIdentity.GetAnyString()}@{meta.HealthUri}");
                    }
                }
                else
                {
                    logger.Warn($"Unable to disconnect from unknown node [{payload.NodeIdentity.GetAnyString()}]");
                }
            }

            return shouldHandle;
        }

        private bool ProcessCheckStalePeersMessage(IMessage message)
        {
            var shouldHandle = message.Equals(KinoMessages.CheckStalePeers);
            if (shouldHandle)
            {
                var staleNodes = connectedPeerRegistry.GetStalePeers();
                if (staleNodes.Any())
                {
                    logger.Debug($"Stale nodes detected: {staleNodes.Count()}. Connectivity check scheduled.");
                    Task.Factory.StartNew(() => CheckConnectivity(cancellationTokenSource.Token, staleNodes), TaskCreationOptions.LongRunning);
                }
            }

            return shouldHandle;
        }

        private bool ProcessCheckPeerConnectionMessage(IMessage message)
        {
            var shouldHandle = message.Equals(KinoMessages.CheckPeerConnection);
            if (shouldHandle)
            {
                var suspiciousNode = new ReceiverIdentifier(message.GetPayload<CheckPeerConnectionMessage>().SocketIdentity);
                logger.Debug($"Connectivity check requested for node {suspiciousNode}");
                var meta = connectedPeerRegistry.Find(suspiciousNode);
                if (meta != null)
                {
                    CheckPeerConnection(suspiciousNode, meta);
                }
            }

            return shouldHandle;
        }

        private void CheckConnectivity(CancellationToken token, Bcl.IEnumerable<Bcl.KeyValuePair<ReceiverIdentifier, ClusterMemberMeta>> staleNodes)
        {
            try
            {
                for (var i = 0; i < staleNodes.Count() && !token.IsCancellationRequested; i++)
                {
                    var staleNode = staleNodes.ElementAt(i);
                    CheckPeerConnection(staleNode.Key, staleNode.Value);
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private void CheckPeerConnection(ReceiverIdentifier nodeIdentifier, ClusterMemberMeta meta)
        {
            if (!meta.ConnectionEstablished)
            {
                using (var socket = socketFactory.CreateRouterSocket())
                {
                    var uri = new Uri(meta.ScaleOutUri);
                    try
                    {
                        socket.SetMandatoryRouting();
                        socket.Connect(uri, waitUntilConnected: true);
                        var message = Message.Create(new PingMessage())
                                             .As<Message>();
                        message.SetDomain(securityProvider.GetDomain(KinoMessages.Ping.Identity));
                        message.SetSocketIdentity(nodeIdentifier.Identity);
                        message.SignMessage(securityProvider);
                        socket.SendMessage(message);
                        socket.Disconnect(uri);
                        meta.LastKnownHeartBeat = DateTime.UtcNow;
                    }
                    catch (Exception err)
                    {
                        routerLocalSocket.Send(Message.Create(new UnregisterUnreachableNodeMessage {ReceiverNodeIdentity = nodeIdentifier.Identity}));
                        logger.Warn($"Failed trying to check connectivity to node {nodeIdentifier}@{uri.ToSocketAddress()}. Peer deletion scheduled. {err}");
                    }
                }
            }
        }

        private bool ProcessCheckDeadPeersMessage(IMessage message)
        {
            var shouldHandle = message.Equals(KinoMessages.CheckDeadPeers);
            if (shouldHandle)
            {
                var deadNodes = connectedPeerRegistry.GetPeersWithExpiredHeartBeat();
                foreach (var deadNode in deadNodes)
                {
                    routerLocalSocket.Send(Message.Create(new UnregisterUnreachableNodeMessage
                                                          {
                                                              ReceiverNodeIdentity = deadNode.Key.Identity
                                                          }));
                    logger.Debug($"Unreachable node {deadNode.Key}@{deadNode.Value.ScaleOutUri} " +
                                 $"with LastKnownHeartBeat {deadNode.Value.LastKnownHeartBeat} detected. " +
                                 "Route deletion scheduled.");
                }
            }

            return shouldHandle;
        }

        private bool ProcessAddPeerMessage(IMessage message, ISocket _)
        {
            var shouldHandle = message.Equals(KinoMessages.AddPeer);
            if (shouldHandle)
            {
                var payload = message.GetPayload<AddPeerMessage>();

                logger.Debug($"New node {payload.SocketIdentity.GetAnyString()} added.");

                var meta = new ClusterMemberMeta
                           {
                               HealthUri = payload.Health.Uri,
                               HeartBeatInterval = payload.Health.HeartBeatInterval,
                               ScaleOutUri = payload.Uri,
                               LastKnownHeartBeat = DateTime.UtcNow
                           };
                connectedPeerRegistry.FindOrAdd(new ReceiverIdentifier(payload.SocketIdentity), meta);
            }

            return shouldHandle;
        }

        private bool ProcessStartPeerMonitoringMessage(IMessage message, ISocket socket)
        {
            var shouldHandle = message.Equals(KinoMessages.StartPeerMonitoring);
            if (shouldHandle)
            {
                var payload = message.GetPayload<StartPeerMonitoringMessage>();

                logger.Debug($"Received {typeof(StartPeerMonitoringMessage).Name} for node {payload.SocketIdentity.GetAnyString()}@{payload.Uri}. "
                             + $"HealthUri: {payload.Health.Uri}");

                var meta = new ClusterMemberMeta
                           {
                               HealthUri = payload.Health.Uri,
                               HeartBeatInterval = payload.Health.HeartBeatInterval,
                               ScaleOutUri = payload.Uri
                           };
                meta = connectedPeerRegistry.FindOrAdd(new ReceiverIdentifier(payload.SocketIdentity), meta);
                // NOTE: Starting peer monitoring may happen after quite some time after it was first added.
                // To avoid immediate node disconnection, as being dead, update LastKnownHeartBeat before setting ConnectionEstablished to TRUE.
                meta.LastKnownHeartBeat = DateTime.UtcNow;
                meta.ConnectionEstablished = true;
                StartDeadPeersCheck(meta.HeartBeatInterval);
                socket.Connect(new Uri(meta.HealthUri));

                logger.Debug($"Connected to node {payload.SocketIdentity.GetAnyString()}@{meta.HealthUri} for HeartBeat monitoring.");
            }

            return shouldHandle;
        }

        private void StartDeadPeersCheck(TimeSpan newHeartBeatInterval)
        {
            if (newHeartBeatInterval < deadPeersCheckInterval)
            {
                deadPeersCheckInterval = newHeartBeatInterval;
                deadPeersCheckObserver?.Dispose();
                deadPeersCheckObserver = Observable.Interval(deadPeersCheckInterval)
                                                   .Subscribe(_ => CheckDeadPeers());
            }
        }

        private void CheckDeadPeers()
        {
            try
            {
                multiplexingSocket.Send(Message.Create(new CheckDeadPeersMessage()));
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private void CheckStalePeers()
        {
            try
            {
                multiplexingSocket.Send(Message.Create(new CheckStalePeersMessage()));
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private bool ProcessHeartBeatMessage(IMessage message, ISocket socket)
        {
            var shouldHandle = message.Equals(KinoMessages.HeartBeat);
            if (shouldHandle)
            {
                var payload = message.GetPayload<HeartBeatMessage>();
                var socketIdentifier = new ReceiverIdentifier(payload.SocketIdentity);
                var meta = connectedPeerRegistry.Find(socketIdentifier);
                if (meta != null)
                {
                    meta.LastKnownHeartBeat = DateTime.UtcNow;
                    //logger.Debug($"Received HeartBeat from node {socketIdentifier}");
                }
                else
                {
                    //TODO: Send DiscoveryMessage? What if peer is not supporting message Domains to be used by this node?
                    logger.Warn($"HeartBeat came from unknown node {payload.SocketIdentity.GetAnyString()}. Will disconnect from HealthUri: {payload.HealthUri}");
                    try
                    {
                        socket.Disconnect(new Uri(payload.HealthUri));
                    }
                    catch (Exception err)
                    {
                        logger.Error(err);
                    }
                }
            }

            return shouldHandle;
        }

        private ISocket CreateSubscriberSocket()
        {
            var socket = socketFactory.CreateSubscriberSocket();
            socket.Connect(config.IntercomEndpoint, waitUntilConnected: true);
            socket.Subscribe();

            return socket;
        }

        private ISocket CreatePublisherSocket()
        {
            var socket = socketFactory.CreatePublisherSocket();
            socket.Bind(config.IntercomEndpoint);

            return socket;
        }
    }
}