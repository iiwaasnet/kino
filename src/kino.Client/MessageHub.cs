using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using MessageContract = kino.Core.Connectivity.MessageContract;

namespace kino.Client
{
    public partial class MessageHub : IMessageHub
    {
        private readonly ICallbackHandlerStack callbackHandlers;
        private Task sending;
        private Task receiving;
        private readonly BlockingCollection<CallbackRegistration> registrationsQueue;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly ManualResetEventSlim hubRegistered;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ILocalSocket<IMessage> localRouterSocket;
        private readonly ILocalSendingSocket<InternalRouteRegistration> internalRegistrationsSender;
        private readonly bool keepRegistrationLocal;
        private readonly ILogger logger;
        private static readonly TimeSpan TerminationWaitTimeout = TimeSpan.FromSeconds(3);
        private static readonly MessageIdentifier ExceptionMessageIdentifier = new MessageIdentifier(KinoMessages.Exception);
        private long lastCallbackKey = 0;
        private readonly ILocalSocket<IMessage> receivingSocket;
        private readonly byte[] receivingSocketIdentity;

        public MessageHub(ICallbackHandlerStack callbackHandlers,
                          IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                          ILocalSocket<IMessage> localRouterSocket,
                          ILocalSendingSocket<InternalRouteRegistration> internalRegistrationsSender,
                          ILocalSocketFactory localSocketFactory,
                          bool keepRegistrationLocal,
                          ILogger logger)
        {
            this.logger = logger;
            this.performanceCounterManager = performanceCounterManager;
            this.localRouterSocket = localRouterSocket;
            this.internalRegistrationsSender = internalRegistrationsSender;
            this.keepRegistrationLocal = keepRegistrationLocal;
            hubRegistered = new ManualResetEventSlim();
            this.callbackHandlers = callbackHandlers;
            registrationsQueue = new BlockingCollection<CallbackRegistration>(new ConcurrentQueue<CallbackRegistration>());
            cancellationTokenSource = new CancellationTokenSource();
            receivingSocket = localSocketFactory.Create<IMessage>();
            receivingSocketIdentity = receivingSocket.GetIdentity().Identity;
        }

        public void Start()
        {
            receiving = Task.Factory.StartNew(_ => ReadReplies(cancellationTokenSource.Token),
                                              cancellationTokenSource.Token,
                                              TaskCreationOptions.LongRunning);
            sending = Task.Factory.StartNew(_ => SendClientRequests(cancellationTokenSource.Token),
                                            cancellationTokenSource.Token,
                                            TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            sending?.Wait(TerminationWaitTimeout);
            receiving?.Wait(TerminationWaitTimeout);
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
                            var callbackKey = lastCallbackKey++;
                            message.RegisterCallbackPoint(receivingSocketIdentity, callbackPoint.MessageIdentifiers, callbackKey);

                            callbackHandlers.Push(callbackKey,
                                                  promise,
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
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (WaitHandle.WaitAny(new[]
                                               {
                                                   receivingSocket.CanReceive(),
                                                   token.WaitHandle
                                               }) == 0)
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
        }

        private void RegisterMessageHub()
        {
            var registration = new InternalRouteRegistration
                               {
                                   MessageContracts = new[]
                                                      {
                                                          new MessageContract
                                                          {
                                                              Identifier = new AnyIdentifier(receivingSocketIdentity),
                                                              KeepRegistrationLocal = keepRegistrationLocal
                                                          }
                                                      },
                                   DestinationSocket = receivingSocket
                               };
            internalRegistrationsSender.Send(registration);
            hubRegistered.Set();
        }

        public IPromise EnqueueRequest(IMessage message, CallbackPoint callbackPoint)
            => InternalEnqueueRequest(message, callbackPoint);

        public void SendOneWay(IMessage message)
            => registrationsQueue.Add(new CallbackRegistration {Message = message});

        private IPromise InternalEnqueueRequest(IMessage message, CallbackPoint callbackPoint)
        {
            hubRegistered.Wait();

            var promise = new Promise();
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