using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using kino.Consensus.Configuration;
using kino.Core.Diagnostics;
using kino.Core.Messaging;
using kino.Core.Sockets;

namespace kino.Consensus
{
    public class IntercomMessageHub : IIntercomMessageHub
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task multicastReceiving;
        private Task unicastReceiving;
        private Task sending;
        private Task notifyListeners;
        private readonly ISynodConfiguration synodConfig;
        private readonly BlockingCollection<IMessage> inMessageQueue;
        private readonly BlockingCollection<IntercomMessage> outMessageQueue;
        private readonly ISocketFactory socketFactory;
        private readonly ILogger logger;
        private static readonly byte[] All = new byte[0];
        private readonly ConcurrentDictionary<Listener, object> subscriptions;
        private static readonly TimeSpan TerminationWaitTimeout = TimeSpan.FromSeconds(3);

        public IntercomMessageHub(ISocketFactory socketFactory,
                                  ISynodConfiguration synodConfig,
                                  ILogger logger)
        {
            this.socketFactory = socketFactory;
            this.logger = logger;
            cancellationTokenSource = new CancellationTokenSource();
            this.synodConfig = synodConfig;
            inMessageQueue = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
            outMessageQueue = new BlockingCollection<IntercomMessage>(new ConcurrentQueue<IntercomMessage>());
            subscriptions = new ConcurrentDictionary<Listener, object>();
        }

        public void Start()
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

                gateway.SignalAndWait(cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            inMessageQueue.CompleteAdding();
            multicastReceiving.Wait(TerminationWaitTimeout);
            unicastReceiving.Wait(TerminationWaitTimeout);
            sending.Wait(TerminationWaitTimeout);
            notifyListeners.Wait(TerminationWaitTimeout);
            inMessageQueue.Dispose();
            cancellationTokenSource.Dispose();
        }

        private void SendMessages(CancellationToken token, Barrier gateway)
        {
            using (var socket = CreateSendingSocket())
            {
                gateway.SignalAndWait(token);

                foreach (var intercomMessage in outMessageQueue.GetConsumingEnumerable(token))
                {
                    var message = (Message) intercomMessage.Message;
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

        private ISocket CreateMulticastListeningSocket()
        {
            var socket = socketFactory.CreateSubscriberSocket();
            socket.Subscribe();
            foreach (var node in synodConfig.Synod)
            {
                socket.Connect(node);
            }

            return socket;
        }

        private ISocket CreateUnicastListeningSocket()
        {
            var socket = socketFactory.CreateSubscriberSocket();
            socket.Subscribe(synodConfig.LocalNode.SocketIdentity);
            foreach (var node in synodConfig.Synod)
            {
                socket.Connect(node);
            }

            return socket;
        }

        private ISocket CreateSendingSocket()
        {
            var socket = socketFactory.CreatePublisherSocket();
            socket.Bind(synodConfig.LocalNode.Uri);

            return socket;
        }

        public Listener Subscribe()
        {
            var listener = new Listener(Unsubscribe, logger);
            subscriptions.TryAdd(listener, null);

            return listener;
        }

        public void Broadcast(IMessage message)
        {
            outMessageQueue.Add(new IntercomMessage {Message = message, Receiver = All});
        }

        public void Send(IMessage message, byte[] receiver)
        {
            outMessageQueue.Add(new IntercomMessage {Message = message, Receiver = receiver});
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
        {
            object obj;
            subscriptions.TryRemove(listener, out obj);
        }

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