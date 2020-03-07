using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using kino.Security;
using MessageContract = kino.Routing.MessageContract;

namespace kino.Actors
{
    public partial class ActorHost : IActorHost
    {
        private readonly IActorHandlerMap actorHandlerMap;
        private Task syncProcessing;
        private Task asyncProcessing;
        private Task registrationsProcessing;
        private CancellationTokenSource cancellationTokenSource;
        private readonly IAsyncQueue<AsyncMessageContext> asyncQueue;
        private readonly ISecurityProvider securityProvider;
        private readonly ILocalSocket<IMessage> localRouterSocket;
        private readonly ILocalSendingSocket<InternalRouteRegistration> internalRegistrationsSender;
        private readonly IAsyncQueue<ActorRegistration> actorRegistrationsQueue;
        private readonly ILogger logger;
        private static readonly TimeSpan TerminationWaitTimeout = TimeSpan.FromSeconds(3);
        private readonly ILocalSocket<IMessage> receivingSocket;

        public ActorHost(IActorHandlerMap actorHandlerMap,
                         IAsyncQueue<AsyncMessageContext> asyncQueue,
                         IAsyncQueue<ActorRegistration> actorRegistrationsQueue,
                         ISecurityProvider securityProvider,
                         ILocalSocketFactory localSocketFactory,
                         ILogger logger)
        {
            this.logger = logger;
            this.actorHandlerMap = actorHandlerMap;
            this.securityProvider = securityProvider;
            localRouterSocket = localSocketFactory.CreateNamed<IMessage>(NamedSockets.RouterLocalSocket);
            internalRegistrationsSender = localSocketFactory.CreateNamed<InternalRouteRegistration>(NamedSockets.InternalRegistrationSocket);
            this.asyncQueue = asyncQueue;
            this.actorRegistrationsQueue = actorRegistrationsQueue;
            receivingSocket = localSocketFactory.Create<IMessage>();
        }

        public void AssignActor(IActor actor)
        {
            var registrations = actorHandlerMap.Add(actor);
            if (registrations.Any())
            {
                var actorRegistration = new ActorRegistration
                                        {
                                            ActorIdentifier = actor.Identifier,
                                            MessageHandlers = registrations
                                        };
                actorRegistrationsQueue.Enqueue(actorRegistration, cancellationTokenSource?.Token ?? CancellationToken.None);
            }
            else
            {
                logger.Warn($"Actor {actor.GetType().FullName}:{actor.Identifier} seems to not handle any message!");
            }
        }

        public bool CanAssignActor(IActor actor)
            => actorHandlerMap.CanAdd(actor);

        public void Start()
        {
            using (var barrier = new Barrier(2))
            {
                cancellationTokenSource = new CancellationTokenSource();
                registrationsProcessing = Task.Factory.StartNew(_ => SyntaxSugar.SafeExecuteUntilCanceled(() => RegisterActors(cancellationTokenSource.Token), logger),
                                                                cancellationTokenSource.Token,
                                                                TaskCreationOptions.LongRunning);
                syncProcessing = Task.Factory.StartNew(_ => SyntaxSugar.SafeExecuteUntilCanceled(() => ProcessRequests(cancellationTokenSource.Token, barrier), logger),
                                                       cancellationTokenSource.Token,
                                                       TaskCreationOptions.LongRunning);
                asyncProcessing = Task.Factory.StartNew(_ => SyntaxSugar.SafeExecuteUntilCanceled(() => ProcessAsyncResponses(cancellationTokenSource.Token), logger),
                                                        cancellationTokenSource.Token,
                                                        TaskCreationOptions.LongRunning);

                barrier.SignalAndWait(cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            cancellationTokenSource?.Cancel();
            registrationsProcessing?.Wait(TerminationWaitTimeout);
            syncProcessing?.Wait(TerminationWaitTimeout);
            asyncProcessing?.Wait(TerminationWaitTimeout);
            cancellationTokenSource?.Dispose();
        }

        private void RegisterActors(CancellationToken token)
        {
            try
            {
                foreach (var registration in actorRegistrationsQueue.GetConsumingEnumerable(token))
                {
                    try
                    {
                        SendActorRegistrationMessage(registration);
                    }
                    catch (Exception err)
                    {
                        logger.Error(err);
                    }
                }
            }
            finally
            {
                actorRegistrationsQueue.Dispose();
            }
        }

        private void SendActorRegistrationMessage(ActorRegistration registration)
        {
            var routeReg = new InternalRouteRegistration
                           {
                               ReceiverIdentifier = registration.ActorIdentifier,
                               MessageContracts = registration.MessageHandlers
                                                              .Select(mh => new MessageContract
                                                                            {
                                                                                Message = mh.Identifier,
                                                                                KeepRegistrationLocal = mh.KeepRegistrationLocal
                                                                            })
                                                              .ToArray(),
                               DestinationSocket = receivingSocket
                           };

            internalRegistrationsSender.Send(routeReg);
        }

        private void ProcessRequests(CancellationToken token, Barrier barrier)
        {
            try
            {
                var waitHandles = new[]
                                  {
                                      receivingSocket.CanReceive(),
                                      token.WaitHandle
                                  };
                barrier.SignalAndWait(token);

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (WaitHandle.WaitAny(waitHandles) == 0)
                        {
                            var message = (Message) receivingSocket.TryReceive();
                            if (message != null)
                            {
                                try
                                {
                                    var actorIdentifier = new MessageIdentifier(message);
                                    var handler = actorHandlerMap.Get(actorIdentifier);
                                    if (handler != null)
                                    {
                                        var task = handler(message);

                                        HandleTaskResult(token, task, message);
                                    }
                                    else
                                    {
                                        HandlerNotFound(message);
                                    }
                                }
                                catch (Exception err)
                                {
                                    //TODO: Add more context to exception about which Actor failed
                                    CallbackException(err, message);
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
            }
            catch (Exception err)
            {
                logger.Error(err);
            }

            logger.Warn($"{GetType().Name} requests processing stopped.");
        }

        private void ProcessAsyncResponses(CancellationToken token)
        {
            try
            {
                foreach (var messageContext in asyncQueue.GetConsumingEnumerable(token))
                {
                    try
                    {
                        foreach (var messageOut in messageContext.OutMessages.Cast<Message>())
                        {
                            messageOut.SetDomain(securityProvider.GetDomain(messageOut.Identity));
                            if (messageOut.Distribution == DistributionPattern.Unicast)
                            {
                                messageOut.RegisterCallbackPoint(messageContext.CallbackReceiverNodeIdentity,
                                                                 messageContext.CallbackReceiverIdentity,
                                                                 messageContext.CallbackPoint,
                                                                 messageContext.CallbackKey);
                            }

                            messageOut.SetCorrelationId(messageContext.CorrelationId);
                            messageOut.CopyMessageRouting(messageContext.MessageHops);
                            messageOut.TraceOptions |= messageContext.TraceOptions;

                            localRouterSocket.Send(messageOut);

                            ResponseSent(messageOut, false);
                        }
                    }
                    catch (Exception err)
                    {
                        logger.Error(err);
                    }
                }
            }
            finally
            {
                asyncQueue.Dispose();
            }
        }

        private async Task HandleTaskResult(CancellationToken token, ValueTask<IActorResult> task, Message messageIn)
        {
            if (task != default)
            {
                if (task.IsCompleted)
                {
                    var response = (await CreateTaskResultMessage(task)).Messages;

                    MessageProcessed(messageIn, response);

                    foreach (var messageOut in response.Cast<Message>())
                    {
                        messageOut.SetDomain(securityProvider.GetDomain(messageOut.Identity));
                        if (messageOut.Distribution == DistributionPattern.Unicast)
                        {
                            messageOut.RegisterCallbackPoint(messageIn.CallbackReceiverNodeIdentity,
                                                             messageIn.CallbackReceiverIdentity,
                                                             messageIn.CallbackPoint,
                                                             messageIn.CallbackKey);
                        }

                        messageOut.SetCorrelationId(messageIn.CorrelationId);
                        messageOut.CopyMessageRouting(messageIn.GetMessageRouting());
                        messageOut.TraceOptions |= messageIn.TraceOptions;

                        localRouterSocket.Send(messageOut);

                        ResponseSent(messageOut, true);
                    }
                }
                else
                {
                    SyntaxSugar.SafeExecuteUntilCanceled(() => EnqueueTaskForCompletion(token, task, messageIn), logger);
                }
            }
        }

        private void CallbackException(Exception err, Message messageIn)
        {
            var messageOut = Message.Create(new ExceptionMessage
                                            {
                                                Message = err.Message,
                                                ExceptionType = err.GetType().ToString(),
                                                StackTrace = err.StackTrace
                                            })
                                    .As<Message>();
            messageOut.SetDomain(securityProvider.GetDomain(KinoMessages.Exception.Identity));
            messageOut.RegisterCallbackPoint(messageIn.CallbackReceiverNodeIdentity,
                                             messageIn.CallbackReceiverIdentity,
                                             messageIn.CallbackPoint,
                                             messageIn.CallbackKey);
            messageOut.SetCorrelationId(messageIn.CorrelationId);
            messageOut.CopyMessageRouting(messageIn.GetMessageRouting());
            messageOut.TraceOptions |= messageIn.TraceOptions;

            localRouterSocket.Send(messageOut);
        }

        private async Task EnqueueTaskForCompletion(CancellationToken token, ValueTask<IActorResult> task, Message messageIn)
        {
            var asyncMessageContext = new AsyncMessageContext
                                      {
                                          OutMessages = (await CreateTaskResultMessage(task)).Messages,
                                          CallbackPoint = messageIn.CallbackPoint,
                                          CallbackKey = messageIn.CallbackKey,
                                          CallbackReceiverIdentity = messageIn.CallbackReceiverIdentity,
                                          CallbackReceiverNodeIdentity = messageIn.CallbackReceiverNodeIdentity,
                                          CorrelationId = messageIn.CorrelationId,
                                          MessageHops = messageIn.GetMessageRouting(),
                                          TraceOptions = messageIn.TraceOptions
                                      };
            asyncQueue.Enqueue(asyncMessageContext, token);
        }

        //private IActorResult CreateTaskResultMessage(Task<IActorResult> task)
        //    => task.IsCanceled
        //           ? CreateCancelledTaskResultMessage()
        //           : task.IsFaulted
        //               ? CreateFaultedTaskResultMessage(task.Exception?.GetBaseException()
        //                                             ?? new Exception("Task failed for an unknown reason!"))
        //               : task.Result ?? ActorResult.Empty;

        private async Task<IActorResult> CreateTaskResultMessage(ValueTask<IActorResult> task)
        {
            try
            {
                var res = await task;

                return res ?? ActorResult.Empty;
            }
            catch (OperationCanceledException)
            {
                return CreateCancelledTaskResultMessage();
            }
            catch (Exception e)
            {
                return CreateFaultedTaskResultMessage(e.GetBaseException());
            }
        }

        private IActorResult CreateFaultedTaskResultMessage(Exception err)
        {
            var message = Message.Create(new ExceptionMessage
                                         {
                                             Message = err.Message,
                                             ExceptionType = err.GetType().ToString(),
                                             StackTrace = err.StackTrace
                                         })
                                 .As<Message>();
            message.SetDomain(securityProvider.GetDomain(KinoMessages.Exception.Identity));
            return new ActorResult(message);
        }

        private IActorResult CreateCancelledTaskResultMessage()
        {
            var err = new OperationCanceledException();
            var message = Message.Create(new ExceptionMessage
                                         {
                                             Message = err.Message,
                                             ExceptionType = err.GetType().ToString(),
                                             StackTrace = err.StackTrace
                                         })
                                 .As<Message>();
            message.SetDomain(securityProvider.GetDomain(KinoMessages.Exception.Identity));

            return new ActorResult(message);
        }
    }
}