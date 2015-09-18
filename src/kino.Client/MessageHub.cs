using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Diagnostics;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Sockets;

namespace kino.Client
{
    public class MessageHub : IMessageHub
    {
        private readonly ICallbackHandlerStack callbackHandlers;
        private readonly ISocketFactory socketFactory;
        private Task sending;
        private Task receiving;
        private readonly TaskCompletionSource<byte[]> receivingSocketIdentityPromise;
        private readonly BlockingCollection<CallbackRegistration> registrationsQueue;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly ManualResetEventSlim hubRegistered;
        private readonly MessageHubConfiguration config;
        private readonly ILogger logger;
        private readonly IMessageTracer messageTracer;
        private static readonly TimeSpan TerminationWaitTimeout = TimeSpan.FromSeconds(3);

        public MessageHub(ISocketFactory socketFactory,
                          ICallbackHandlerStack callbackHandlers,
                          MessageHubConfiguration config,
                          IMessageTracer messageTracer,
                          ILogger logger)
        {
            this.logger = logger;
            this.messageTracer = messageTracer;
            this.socketFactory = socketFactory;
            this.config = config;
            receivingSocketIdentityPromise = new TaskCompletionSource<byte[]>();
            hubRegistered = new ManualResetEventSlim();
            this.callbackHandlers = callbackHandlers;
            registrationsQueue = new BlockingCollection<CallbackRegistration>(new ConcurrentQueue<CallbackRegistration>());
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            const int participantCount = 3;
            using (var gateway = new Barrier(participantCount))
            {
                receiving = Task.Factory.StartNew(_ => ReadReplies(cancellationTokenSource.Token, gateway),
                                                  cancellationTokenSource.Token,
                                                  TaskCreationOptions.LongRunning);
                sending = Task.Factory.StartNew(_ => SendClientRequests(cancellationTokenSource.Token, gateway),
                                                cancellationTokenSource.Token,
                                                TaskCreationOptions.LongRunning);

                gateway.SignalAndWait(cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            sending.Wait(TerminationWaitTimeout);
            receiving.Wait(TerminationWaitTimeout);
        }

        private void SendClientRequests(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var socket = CreateOneWaySocket())
                {
                    var receivingSocketIdentity = receivingSocketIdentityPromise.Task.Result;
                    RegisterMessageHub(socket, receivingSocketIdentity);
                    gateway.SignalAndWait(token);

                    foreach (var callbackRegistration in registrationsQueue.GetConsumingEnumerable(token))
                    {
                        try
                        {
                            var message = (Message) callbackRegistration.Message;
                            if (CallbackRequired(callbackRegistration))
                            {
                                var promise = callbackRegistration.Promise;
                                var callbackPoint = callbackRegistration.CallbackPoint;

                                message.RegisterCallbackPoint(callbackPoint.MessageIdentity, receivingSocketIdentity);

                                callbackHandlers.Push(new CorrelationId(message.CorrelationId),
                                                      promise,
                                                      new[]
                                                      {
                                                          new Connectivity.MessageIdentifier(message.Version, callbackPoint.MessageIdentity),
                                                          new Connectivity.MessageIdentifier(message.Version, ExceptionMessage.MessageIdentity)
                                                      });
                                messageTracer.CallbackRegistered(message);
                            }
                            socket.SendMessage(message);
                            messageTracer.SentToRouter(message);
                        }
                        catch (Exception err)
                        {
                            logger.Error(err);
                        }
                    }
                    registrationsQueue.Dispose();
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

        private bool CallbackRequired(CallbackRegistration callbackRegistration)
        {
            return callbackRegistration.Promise != null && callbackRegistration.CallbackPoint != null;
        }

        private void ReadReplies(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var socket = CreateRoutableSocket())
                {
                    receivingSocketIdentityPromise.SetResult(socket.GetIdentity());
                    gateway.SignalAndWait(token);

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var message = socket.ReceiveMessage(token);
                            if (message != null)
                            {
                                var callback = (Promise) callbackHandlers.Pop(new CallbackHandlerKey
                                                                              {
                                                                                  Version = message.Version,
                                                                                  Identity = message.Identity,
                                                                                  Correlation = message.CorrelationId
                                                                              });
                                if (callback != null)
                                {
                                    callback.SetResult(message);
                                    messageTracer.CallbackResultSet(message);
                                }
                                else
                                {
                                    messageTracer.CallbackNotFound(message);
                                }
                            }
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
        }

        private ISocket CreateOneWaySocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(config.RouterUri);

            return socket;
        }

        private ISocket CreateRoutableSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.SetIdentity(SocketIdentifier.CreateNew());
            socket.Connect(config.RouterUri);

            return socket;
        }

        private void RegisterMessageHub(ISocket socket, byte[] receivingSocketIdentity)
        {
            var rdyMessage = Message.Create(new RegisterInternalMessageRouteMessage
                                            {
                                                SocketIdentity = receivingSocketIdentity,
                                                MessageContracts = new[]
                                                                  {
                                                                      new MessageContract
                                                                      {
                                                                          Version = Message.CurrentVersion,
                                                                          Identity = receivingSocketIdentity
                                                                      }
                                                                  }
                                            },
                                            RegisterInternalMessageRouteMessage.MessageIdentity);
            socket.SendMessage(rdyMessage);

            hubRegistered.Set();
        }

        public IPromise EnqueueRequest(IMessage message, ICallbackPoint callbackPoint)
        {
            return InternalEnqueueRequest(message, callbackPoint);
        }

        public IPromise EnqueueRequest(IMessage message, ICallbackPoint callbackPoint, TimeSpan expireAfter)
        {
            return InternalEnqueueRequest(message, callbackPoint, expireAfter);
        }

        public void SendOneWay(IMessage message)
        {
            registrationsQueue.Add(new CallbackRegistration {Message = message});
        }

        private IPromise InternalEnqueueRequest(IMessage message, ICallbackPoint callbackPoint, TimeSpan? expireAfter = null)
        {
            hubRegistered.Wait();

            var promise = (expireAfter != null)
                              ? new Promise(expireAfter.Value)
                              : new Promise();

            registrationsQueue.Add(new CallbackRegistration
                                   {
                                       Message = message,
                                       Promise = promise,
                                       CallbackPoint = callbackPoint
                                   });

            return promise;
        }
    }
}