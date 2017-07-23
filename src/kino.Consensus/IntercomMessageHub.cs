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
            nodeHealthInfoMap = synodConfig.Synod
                                           .ToDictionary(n => n.ToSocketAddress(), _ => new NodeHealthInfo {LastKnownHeartBeat = DateTime.UtcNow});
        }

        public bool Start(TimeSpan startTimeout)
        {
            const int participantsCount = 5;
            using (var gateway = new Barrier(participantsCount))
            {
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
                heartBeating = StartHeartBeating();

                return gateway.SignalAndWait(startTimeout, cancellationTokenSource.Token);
            }
        }

        private Timer StartHeartBeating()
        {
            var timer = new Timer(_ => SendHeartBeats(), null, TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
            var nodesInCluster = synodConfig.Synod.Count();
            if (nodesInCluster > 1)
            {
                timer.Change(synodConfig.HeartBeatInterval, synodConfig.HeartBeatInterval);

                logger.Info($"Consensus HeartBeating started. Number of nodes in cluster: {nodesInCluster}");
            }
            else
            {
                logger.Warn($"Consensus HeartBeating disabled. Number of nodes in cluster: {nodesInCluster}");
            }

            return timer;
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
                            inMessageQueue.Add(message, token);
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

        private void SendHeartBeats()
        {
            try
            {
                Broadcast(Message.Create(new HeartBeatMessage {NodeUri = synodConfig.LocalNode.Uri.ToSocketAddress()}));
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private ISocket CreateMulticastListeningSocket()
        {
            var socket = socketFactory.CreateSubscriberSocket();
            socket.ReceiveRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.IntercomMulticastSocketReceiveRate);
            socket.Subscribe();
            foreach (var node in synodConfig.Synod)
            {
                socket.Connect(node, waitUntilConnected: true);

                logger.Info($"{nameof(IntercomMessageHub)} connected to: {node.ToSocketAddress()} (Multicast)");
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
                socket.Connect(node, waitUntilConnected: true);

                logger.Info($"{nameof(IntercomMessageHub)} connected to: {node.ToSocketAddress()} (Unicast)");
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

        public Listener Subscribe()
        {
            var listener = new Listener(Unsubscribe, logger);
            subscriptions.TryAdd(listener, null);

            return listener;
        }

        public void Broadcast(IMessage message)
            => outMessageQueue.Add(new IntercomMessage {Message = message, Receiver = All});

        public void Send(IMessage message, byte[] receiver)
            => outMessageQueue.Add(new IntercomMessage {Message = message, Receiver = receiver});

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

        //private bool HeartBeatExpired(DateTime now)
        //    => peer.Value.ConnectionEstablished
        //       && now - peer.Value.LastKnownHeartBeat > peer.Value.HeartBeatInterval.MultiplyBy(config.MissingHeartBeatsBeforeDeletion);
    }
}