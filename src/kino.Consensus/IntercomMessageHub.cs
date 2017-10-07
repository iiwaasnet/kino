using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Consensus.Configuration;
using kino.Consensus.Messages;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;

namespace kino.Consensus
{
    public class IntercomMessageHub : IIntercomMessageHub
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task receiving;
        private Task sending;
        private Task notifyListeners;
        private Timer heartBeating;
        private readonly ISynodConfigurationProvider synodConfigProvider;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly BlockingCollection<IMessage> inMessageQueue;
        private readonly BlockingCollection<IMessage> outMessageQueue;
        private readonly ISocketFactory socketFactory;
        private readonly ILogger logger;
        private readonly ConcurrentDictionary<Listener, object> subscriptions;
        private static readonly TimeSpan TerminationWaitTimeout = TimeSpan.FromSeconds(3);
        private readonly IEnumerable<NodeHealthInfo> nodeHealthInfo;
        private ISocket intercomSocket;

        public IntercomMessageHub(ISocketFactory socketFactory,
                                  ISynodConfigurationProvider synodConfigProvider,
                                  IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                  ILogger logger)
        {
            this.socketFactory = socketFactory;
            this.logger = logger;
            cancellationTokenSource = new CancellationTokenSource();
            this.synodConfigProvider = synodConfigProvider;
            this.performanceCounterManager = performanceCounterManager;
            inMessageQueue = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
            outMessageQueue = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
            subscriptions = new ConcurrentDictionary<Listener, object>();
            nodeHealthInfo = CreateNodeHealthInfoMap(synodConfigProvider);
        }

        private static IEnumerable<NodeHealthInfo> CreateNodeHealthInfoMap(ISynodConfigurationProvider synodConfigProvider)
            => synodConfigProvider.Synod
                                  .Where(node => node.Uri != synodConfigProvider.LocalNode.Uri)
                                  .Select(node => new NodeHealthInfo(synodConfigProvider.HeartBeatInterval,
                                                                     synodConfigProvider.MissingHeartBeatsBeforeReconnect,
                                                                     node))
                                  .ToList();

        public bool Start(TimeSpan startTimeout)
        {
            const int participantsCount = 4;
            using (var gateway = new Barrier(participantsCount))
            {
                heartBeating = StartHeartBeating();

                receiving = Task.Factory.StartNew(_ => SafeExecute(() => ReceiveMessages(cancellationTokenSource.Token, gateway)),
                                                  cancellationTokenSource.Token,
                                                  TaskCreationOptions.LongRunning);

                sending = Task.Factory.StartNew(_ => SafeExecute(() => SendMessages(cancellationTokenSource.Token, gateway)),
                                                cancellationTokenSource.Token,
                                                TaskCreationOptions.LongRunning);
                notifyListeners = Task.Factory.StartNew(_ => SafeExecute(() => ForwardIncomingMessages(cancellationTokenSource.Token, gateway)),
                                                        cancellationTokenSource.Token,
                                                        TaskCreationOptions.LongRunning);
                return gateway.SignalAndWait(startTimeout, cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            heartBeating?.Dispose();
            inMessageQueue.CompleteAdding();
            outMessageQueue.CompleteAdding();
            receiving?.Wait(TerminationWaitTimeout);
            sending?.Wait(TerminationWaitTimeout);
            notifyListeners?.Wait(TerminationWaitTimeout);
            inMessageQueue.Dispose();
            outMessageQueue.Dispose();
            cancellationTokenSource.Dispose();
            intercomSocket?.Dispose();
        }

        public void Send(IMessage message)
            => outMessageQueue.Add(message);

        public IEnumerable<INodeHealthInfo> GetClusterHealthInfo()
            => nodeHealthInfo;

        public Listener Subscribe()
        {
            var listener = new Listener(Unsubscribe, logger);
            subscriptions.TryAdd(listener, null);

            return listener;
        }

        private Timer StartHeartBeating()
        {
            var timer = new Timer(_ => SendAndCheckHeartBeats(), null, TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
            if (ShouldDoHeartBeating())
            {
                intercomSocket = CreateIntercomPublisherSocket();

                timer.Change(synodConfigProvider.HeartBeatInterval, synodConfigProvider.HeartBeatInterval);
            }

            logger.Info($"Consensus HeartBeating {(nodeHealthInfo.Any() ? "started" : "disabled")}. "
                        + $"Number of nodes in cluster: {synodConfigProvider.Synod.Count()}");

            return timer;
        }

        private void SendMessages(CancellationToken token, Barrier gateway)
        {
            using (var socket = CreateSendingSocket())
            {
                gateway.SignalAndWait(token);

                foreach (var message in outMessageQueue.GetConsumingEnumerable(token))
                {
                    socket.SendMessage(message);
                }
            }
        }

        private void ReceiveMessages(CancellationToken token, Barrier gateway)
        {
            using (var socket = CreateListeningSocket())
            {
                gateway.SignalAndWait(token);

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var message = socket.ReceiveMessage(token);
                        if (message != null)
                        {
                            if (!ProcessServiceMessage(message, socket))
                            {
                                inMessageQueue.Add(message, token);
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

        private bool ProcessServiceMessage(IMessage message, ISocket socket)
            => ProcessHeartBeatMessage(message)
               || ProcessReconnectMessage(message, socket);

        private bool ProcessReconnectMessage(IMessage message, ISocket socket)
        {
            var shouldHandle = message.Equals(ConsensusMessages.ReconnectClusterMember);
            if (shouldHandle)
            {
                var payload = message.GetPayload<ReconnectClusterMemberMessage>();
                var healthInfo = nodeHealthInfo.FirstOrDefault(hi => hi.NodeUri == new Uri(payload.NewUri));
                if (healthInfo != null)
                {
                    healthInfo.UpdateLastReconnectTime();

                    socket.SafeDisconnect(new Uri(payload.OldUri));
                    socket.Connect(new Uri(payload.NewUri));

                    logger.Info($"Reconnected to node from {payload.OldUri} to {payload.NewUri}");
                }
                else
                {
                    logger.Warn($"{message.Identity.GetAnyString()} came for unknown node: {payload.NewUri}");
                }
            }

            return shouldHandle;
        }

        private bool ProcessHeartBeatMessage(IMessage message)
        {
            var shouldHandle = message.Equals(ConsensusMessages.HeartBeat);
            if (shouldHandle)
            {
                var payload = message.GetPayload<HeartBeatMessage>();
                var healthInfo = nodeHealthInfo.FirstOrDefault(hi => hi.NodeUri == new Uri(payload.NodeUri));
                if (healthInfo != null)
                {
                    healthInfo.UpdateHeartBeat();
                }
                else
                {
                    if (!IsLocalNode(payload.NodeUri))
                    {
                        logger.Warn($"{message.Identity.GetAnyString()} came from unknown node: {payload.NodeUri}");
                    }
                }
            }

            return shouldHandle;

            bool IsLocalNode(string nodeUri)
                => new Uri(nodeUri) == synodConfigProvider.LocalNode.Uri;
        }

        private void SendAndCheckHeartBeats()
        {
            try
            {
                SendHeartBeat();
                CheckDeadNodesAndScheduleReconnect();
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private void CheckDeadNodesAndScheduleReconnect()
        {
            foreach (var unreachable in GetUnreachableNodes())
            {
                var oldUri = unreachable.DynamicUri.Uri;
                unreachable.DynamicUri.Refresh();
                var newUri = unreachable.DynamicUri.Uri;

                ScheduleReconnectSocket(oldUri, newUri);

                var lastKnownHeartBeat = unreachable.HealthInfo.LastKnownHeartBeat;
                logger.Warn($"Reconnect to node {oldUri} scheduled due to old {nameof(lastKnownHeartBeat)}: {lastKnownHeartBeat}");
            }

            IEnumerable<(NodeHealthInfo HealthInfo, DynamicUri DynamicUri)> GetUnreachableNodes()
                => nodeHealthInfo.Where(hi => !hi.IsHealthy() && hi.ShouldReconnect())
                                 .Join(synodConfigProvider.Synod,
                                       hi => hi.NodeUri,
                                       s => s.Uri,
                                       (hi, location) => (HealthInfo: hi, Location: location));
        }

        private void ScheduleReconnectSocket(Uri oldUri, Uri newUri)
        {
            var message = Message.Create(new ReconnectClusterMemberMessage
                                         {
                                             OldUri = oldUri.ToSocketAddress(),
                                             NewUri = newUri.ToSocketAddress()
                                         }).As<Message>();
            intercomSocket.SendMessage(message);
        }

        private void SendHeartBeat()
            => outMessageQueue.Add(Message.Create(new HeartBeatMessage
                                                  {
                                                      NodeUri = synodConfigProvider.LocalNode.Uri.ToSocketAddress()
                                                  }));

        private ISocket CreateListeningSocket()
        {
            var socket = socketFactory.CreateSubscriberSocket();
            socket.ReceiveRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.IntercomMulticastSocketReceiveRate);
            socket.Subscribe();
            foreach (var node in synodConfigProvider.Synod)
            {
                socket.Connect(node.Uri, true);

                logger.Info($"{nameof(IntercomMessageHub)} connected to: {node.Uri.ToSocketAddress()}");
            }
            if (ShouldDoHeartBeating())
            {
                socket.Connect(synodConfigProvider.IntercomEndpoint, true);
            }

            return socket;
        }

        private ISocket CreateSendingSocket()
        {
            var socket = socketFactory.CreatePublisherSocket();
            socket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.IntercomSocketSendRate);
            socket.Bind(synodConfigProvider.LocalNode.Uri);

            logger.Info($"{nameof(IntercomMessageHub)} bound to: {synodConfigProvider.LocalNode.Uri.ToSocketAddress()}");

            return socket;
        }

        private ISocket CreateIntercomPublisherSocket()
        {
            var socket = socketFactory.CreatePublisherSocket();
            socket.Bind(synodConfigProvider.IntercomEndpoint);

            return socket;
        }

        private void ForwardIncomingMessages(CancellationToken token, Barrier gateway)
        {
            gateway.SignalAndWait(token);
            foreach (var message in inMessageQueue.GetConsumingEnumerable(token))
            {
                try
                {
                    foreach (var subscription in subscriptions.Keys)
                    {
                        subscription.Notify(message);
                    }
                }
                catch (Exception err)
                {
                    logger.Error(err);
                }
            }
        }

        private void Unsubscribe(Listener listener)
            => subscriptions.TryRemove(listener, out var _);

        private bool ShouldDoHeartBeating()
            => synodConfigProvider.Synod.Count() > 1;

        private void SafeExecute(Action wrappedMethod)
        {
            try
            {
                wrappedMethod();
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