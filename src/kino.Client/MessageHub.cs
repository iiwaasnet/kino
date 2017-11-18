using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using kino.Security;

namespace kino.Client
{
    public partial class MessageHub : IMessageHub
    {
        private readonly ICallbackHandlerStack callbackHandlers;
        private Task sending;
        private Task receiving;
        private readonly BlockingCollection<CallbackRegistration> registrationsQueue;
        private CancellationTokenSource cancellationTokenSource;
        private readonly ILocalSocket<IMessage> localRouterSocket;
        private readonly ILocalSendingSocket<InternalRouteRegistration> internalRegistrationsSender;
        private readonly IScaleOutConfigurationProvider scaleOutConfigurationProvider;
        private readonly ISecurityProvider securityProvider;
        private readonly bool keepRegistrationLocal;
        private readonly ILogger logger;
        private static readonly TimeSpan TerminationWaitTimeout = TimeSpan.FromSeconds(3);
        private static readonly MessageIdentifier ExceptionMessageIdentifier = new MessageIdentifier(KinoMessages.Exception);
        private long lastCallbackKey = 0;
        private readonly ILocalSocket<IMessage> receivingSocket;
        private byte[] callbackReceiverNodeIdentity;
        private bool isStarted;

        public MessageHub(ICallbackHandlerStack callbackHandlers,
                          ILocalSocket<IMessage> localRouterSocket,
                          ILocalSendingSocket<InternalRouteRegistration> internalRegistrationsSender,
                          ILocalSocketFactory localSocketFactory,
                          IScaleOutConfigurationProvider scaleOutConfigurationProvider,
                          ISecurityProvider securityProvider,
                          ILogger logger,
                          bool keepRegistrationLocal = false)
        {
            this.logger = logger;
            this.localRouterSocket = localRouterSocket;
            this.internalRegistrationsSender = internalRegistrationsSender;
            this.scaleOutConfigurationProvider = scaleOutConfigurationProvider;
            this.securityProvider = securityProvider;
            this.keepRegistrationLocal = keepRegistrationLocal;
            this.callbackHandlers = callbackHandlers;
            registrationsQueue = new BlockingCollection<CallbackRegistration>(new ConcurrentQueue<CallbackRegistration>());
            receivingSocket = localSocketFactory.Create<IMessage>();
            ReceiverIdentifier = ReceiverIdentities.CreateForMessageHub();
        }

        public void Start()
        {
            if (!isStarted)
            {
                cancellationTokenSource = new CancellationTokenSource();
                callbackReceiverNodeIdentity = GetBlockingCallbackReceiverNodeIdentity();
                receiving = Task.Factory.StartNew(_ => ReadReplies(cancellationTokenSource.Token),
                                                  cancellationTokenSource.Token,
                                                  TaskCreationOptions.LongRunning);
                sending = Task.Factory.StartNew(_ => SendClientRequests(cancellationTokenSource.Token),
                                                cancellationTokenSource.Token,
                                                TaskCreationOptions.LongRunning);
                isStarted = true;
            }
        }

        private byte[] GetBlockingCallbackReceiverNodeIdentity()
            => scaleOutConfigurationProvider.GetScaleOutAddress().Identity;

        public void Stop()
        {
            cancellationTokenSource?.Cancel();
            sending?.Wait(TerminationWaitTimeout);
            receiving?.Wait(TerminationWaitTimeout);
            cancellationTokenSource?.Dispose();
            isStarted = false;
        }

        private void SendClientRequests(CancellationToken token)
        {
            try
            {
                RegisterMessageHub();

                foreach (var callbackRegistration in registrationsQueue.GetConsumingEnumerable(token))
                {
                    try
                    {
                        var message = (Message) callbackRegistration.Message;
                        if (CallbackRequired(callbackRegistration))
                        {
                            var promise = callbackRegistration.Promise;
                            var callbackPoint = callbackRegistration.CallbackPoint;
                            message.RegisterCallbackPoint(callbackReceiverNodeIdentity,
                                                          ReceiverIdentifier.Identity,
                                                          callbackPoint.MessageIdentifiers,
                                                          promise.CallbackKey.Value);

                            callbackHandlers.Push(promise,
                                                  callbackPoint.MessageIdentifiers.Concat(new[] {ExceptionMessageIdentifier}));
                            CallbackRegistered(message);
                        }
                        localRouterSocket.Send(message);
                        SentToRouter(message);
                    }
                    catch (Exception err)
                    {
                        logger.Error(err);
                    }
                }
                registrationsQueue.Dispose();
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
            => callbackRegistration.Promise != null && callbackRegistration.CallbackPoint != null;

        private void ReadReplies(CancellationToken token)
        {
            try
            {
                var waitHandles = new[]
                                  {
                                      receivingSocket.CanReceive(),
                                      token.WaitHandle
                                  };
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (WaitHandle.WaitAny(waitHandles) == 0)
                        {
                            var message = receivingSocket.TryReceive().As<Message>();
                            if (message != null)
                            {
                                var callback = (Promise) callbackHandlers.Pop(new CallbackHandlerKey
                                                                              {
                                                                                  Version = message.Version,
                                                                                  Identity = message.Identity,
                                                                                  Partition = message.Partition,
                                                                                  CallbackKey = message.CallbackKey
                                                                              });
                                if (callback != null)
                                {
                                    callback.SetResult(message);
                                    CallbackResultSet(message);
                                }
                                else
                                {
                                    CallbackNotFound(message);
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
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
            logger.Warn($"{GetType().Name} replies reading stopped.");
        }

        private void RegisterMessageHub()
        {
            var registration = new InternalRouteRegistration
                               {
                                   ReceiverIdentifier = ReceiverIdentifier,
                                   KeepRegistrationLocal = keepRegistrationLocal,
                                   DestinationSocket = receivingSocket
                               };
            internalRegistrationsSender.Send(registration);
        }

        public IPromise Send(IMessage message, CallbackPoint callbackPoint)
        {
            message.As<Message>().SetDomain(securityProvider.GetDomain(message.Identity));

            return InternalEnqueueRequest(message, callbackPoint);
        }

        public IPromise Send(IMessage message)
        {
            AssertMessageIsNotBroadcast(message);

            message.As<Message>().SetDomain(securityProvider.GetDomain(message.Identity));

            return InternalEnqueueRequest(message, CallbackPoint.Create<ReceiptConfirmationMessage>());
        }

        private static void AssertMessageIsNotBroadcast(IMessage message)
        {
            if (message.Distribution == DistributionPattern.Broadcast)
            {
                throw new Exception($"{nameof(DistributionPattern.Broadcast)} message can't be sent with receipt confirmation!");
            }
        }

        public void SendOneWay(IMessage message)
        {
            message.As<Message>().SetDomain(securityProvider.GetDomain(message.Identity));
            registrationsQueue.Add(new CallbackRegistration {Message = message});
        }

        private IPromise InternalEnqueueRequest(IMessage message, CallbackPoint callbackPoint)
        {
            var promise = new Promise(Interlocked.Increment(ref lastCallbackKey));
            registrationsQueue.Add(new CallbackRegistration
                                   {
                                       Message = message,
                                       Promise = promise,
                                       CallbackPoint = callbackPoint
                                   });

            return promise;
        }

        public ReceiverIdentifier ReceiverIdentifier { get; }
    }
}