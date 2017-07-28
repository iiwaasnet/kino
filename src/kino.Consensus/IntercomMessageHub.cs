using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Consensus.Configuration;
using kino.Consensus.Messages;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;

namespace kino.Consensus
{
    public class IntercomMessageHub : IIntercomMessageHub
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task multicastReceiving;
        private Task unicastReceiving;
        private Task sending;
        private Task notifyListeners;
        private Timer heartBeating;
        private readonly ISynodConfiguration synodConfig;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly BlockingCollection<IMessage> inMessageQueue;
        private readonly BlockingCollection<IntercomMessage> outMessageQueue;
        private readonly ISocketFactory socketFactory;
        private readonly ILogger logger;
        private static readonly byte[] All = new byte[0];
        private readonly ConcurrentDictionary<Listener, object> subscriptions;
        private static readonly TimeSpan TerminationWaitTimeout = TimeSpan.FromSeconds(3);
        private readonly IDictionary<string, NodeHealthInfo> nodeHealthInfoMap;
        private ISocket intercomSocket;

        public IntercomMessageHub(ISocketFactory socketFactory,
                                  ISynodConfiguration synodConfig,
                                  IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                  ILogger logger)
        {
            this.socketFactory = socketFactory;
            this.logger = logger;
            cancellationTokenSource = new CancellationTokenSource();
            this.synodConfig = synodConfig;
            this.performanceCounterManager = performanceCounterManager;
            inMessageQueue = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
            outMessageQueue = new BlockingCollection<IntercomMessage>(new ConcurrentQueue<IntercomMessage>());
            subscriptions = new ConcurrentDictionary<Listener, object>();
            nodeHealthInfoMap = CreateNodeHealthInfoMap(synodConfig);
        }

        private static Dictionary<string, NodeHealthInfo> CreateNodeHealthInfoMap(ISynodConfiguration synodConfig)
            => synodConfig.Synod
                          .Except(synodConfig.LocalNode.Uri.ToEnumerable())
                          .ToDictionary(node => node.ToSocketAddress(),
                                        node => new NodeHealthInfo(synodConfig.HeartBeatInterval,
                                                                   synodConfig.MissingHeartBeatsBeforeReconnect,
                                                                   node));

        public bool Start(TimeSpan startTimeout)
        {
            const int participantsCount = 5;
            using (var gateway = new Barrier(participantsCount))
            {
                heartBeating = StartHeartBeating(gateway, cancellationTokenSource.Token);

                multicastReceiving = Task.Factory.StartNew(_ => SafeExecute(() => ReceiveMessages(cancellationTokenSource.Token, gateway, CreateMulticastListeningSocket)),
                                                           cancellationTokenSource.Token,
                                                           TaskCreationOptions.LongRunning);
                unicastReceiving = Task.Factory.StartNew(_ => SafeExecute(() => ReceiveMessages(cancellationTokenSource.Token, gateway, CreateUnicastListeningSocket)),
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
            multicastReceiving?.Wait(TerminationWaitTimeout);
            unicastReceiving?.Wait(TerminationWaitTimeout);
            sending?.Wait(TerminationWaitTimeout);
            notifyListeners?.Wait(TerminationWaitTimeout);
            inMessageQueue.Dispose();
            outMessageQueue.Dispose();
            cancellationTokenSource.Dispose();
            intercomSocket?.Dispose();
        }

        public void Broadcast(IMessage message)
            => outMessageQueue.Add(new IntercomMessage {Message = message, Receiver = All});

        public void Send(IMessage message, byte[] receiver)
            => outMessageQueue.Add(new IntercomMessage {Message = message, Receiver = receiver});

        public IEnumerable<INodeHealthInfo> GetClusterHealthInfo()
            => nodeHealthInfoMap.Values;

        public Listener Subscribe()
        {
            var listener = new Listener(Unsubscribe, logger);
            subscriptions.TryAdd(listener, null);

            return listener;
        }

        private Timer StartHeartBeating(Barrier gateway, CancellationToken token)
        {
            var timer = new Timer(_ => SendAndCheckHeartBeats(), null, TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
            if (ShouldDoHeartBeating())
            {
                intercomSocket = CreateIntercomPublisherSocket();

                timer.Change(synodConfig.HeartBeatInterval, synodConfig.HeartBeatInterval);
            }

            logger.Info($"Consensus HeartBeating {(nodeHealthInfoMap.Any() ? "started" : "disabled")}. "
                        + $"Number of nodes in cluster: {synodConfig.Synod.Count()}");

            return timer;
        }

        private void SendMessages(CancellationToken token, Barrier gateway)
        {
            using (var socket = CreateSendingSocket())
            {
                gateway.SignalAndWait(token);

                foreach (var intercomMessage in outMessageQueue.GetConsumingEnumerable(token))
                {
                    var message = intercomMessage.Message.As<Message>();
                    message.SetSocketIdentity(intercomMessage.Receiver);
                    socket.SendMessage(message);
                }
            }
        }

        private void ReceiveMessages(CancellationToken token, Barrier gateway, Func<ISocket> socketBuilder)
        {
            using (var socket = socketBuilder())
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
                if (nodeHealthInfoMap.TryGetValue(payload.NodeUri, out var healthInfo))
                {
                    healthInfo.UpdateLastReconnectTime();

                    socket.Disconnect(new Uri(payload.NodeUri));
                    socket.Connect(new Uri(payload.NodeUri));

                    logger.Info($"Reconnected to node {payload.NodeUri}");
                }
                else
                {
                    logger.Warn($"{message.Identity.GetAnyString()} came for unknown node: {payload.NodeUri}");
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
                if (nodeHealthInfoMap.TryGetValue(payload.NodeUri, out var healthInfo))
                {
                    healthInfo.UpdateHeartBeat();
                }
                else
                {
                    logger.Warn($"{message.Identity.GetAnyString()} came from unknown node: {payload.NodeUri}");
                }
            }

            return shouldHandle;
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
            foreach (var unreachable in nodeHealthInfoMap.Where(node => node.Value.ShouldReconnect()))
            {
                ScheduleReconnectSocket(unreachable.Key, All);
                ScheduleReconnectSocket(unreachable.Key, synodConfig.LocalNode.SocketIdentity);

                var lastKnownHeartBeat = unreachable.Value.LastKnownHeartBeat;
                logger.Warn($"Reconnect to node {unreachable.Key} scheduled due to old {nameof(lastKnownHeartBeat)}: {lastKnownHeartBeat}");
            }
        }

        private void ScheduleReconnectSocket(string uri, byte[] receiver)
        {
            var message = Message.Create(new ReconnectClusterMemberMessage {NodeUri = uri}).As<Message>();
            message.SetSocketIdentity(receiver);
            intercomSocket.SendMessage(message);
        }

        private void SendHeartBeat()
            => Broadcast(Message.Create(new HeartBeatMessage {NodeUri = synodConfig.LocalNode.Uri.ToSocketAddress()}));

        private ISocket CreateMulticastListeningSocket()
        {
            var socket = socketFactory.CreateSubscriberSocket();
            socket.ReceiveRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.IntercomMulticastSocketReceiveRate);
            socket.Subscribe();
            foreach (var node in synodConfig.Synod)
            {
                socket.Connect(node, true);

                logger.Info($"{nameof(IntercomMessageHub)} connected to: {node.ToSocketAddress()} (Multicast)");
            }
            if (ShouldDoHeartBeating())
            {
                socket.Connect(synodConfig.IntercomEndpoint, true);
            }

            return socket;
        }

        private ISocket CreateUnicastListeningSocket()
        {
            var socket = socketFactory.CreateSubscriberSocket();
            socket.ReceiveRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.IntercomUnicastSocketReceiveRate);
            socket.Subscribe(synodConfig.LocalNode.SocketIdentity);
            foreach (var node in synodConfig.Synod)
            {
                socket.Connect(node, true);

                logger.Info($"{nameof(IntercomMessageHub)} connected to: {node.ToSocketAddress()} (Unicast)");
            }
            if (ShouldDoHeartBeating())
            {
                socket.Connect(synodConfig.IntercomEndpoint, true);
            }

            return socket;
        }

        private ISocket CreateSendingSocket()
        {
            var socket = socketFactory.CreatePublisherSocket();
            socket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.IntercomSocketSendRate);
            socket.Bind(synodConfig.LocalNode.Uri);

            logger.Info($"{nameof(IntercomMessageHub)} bound to: {synodConfig.LocalNode.Uri.ToSocketAddress()}");

            return socket;
        }

        private ISocket CreateIntercomPublisherSocket()
        {
            var socket = socketFactory.CreatePublisherSocket();
            socket.Bind(synodConfig.IntercomEndpoint);

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
            => synodConfig.Synod.Count() > 1;

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