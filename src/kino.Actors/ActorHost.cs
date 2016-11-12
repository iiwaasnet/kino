using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
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
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly IAsyncQueue<AsyncMessageContext> asyncQueue;
        private readonly ISecurityProvider securityProvider;
        private readonly ILocalSocket<IMessage> localRouterSocket;
        private readonly ILocalSendingSocket<InternalRouteRegistration> internalRegistrationsSender;
        private readonly IAsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>> actorRegistrationsQueue;
        private readonly ILogger logger;
        private static readonly TimeSpan TerminationWaitTimeout = TimeSpan.FromSeconds(3);
        private readonly ILocalSocket<IMessage> receivingSocket;

        public ActorHost(IActorHandlerMap actorHandlerMap,
                         IAsyncQueue<AsyncMessageContext> asyncQueue,
                         IAsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>> actorRegistrationsQueue,
                         ISecurityProvider securityProvider,
                         ILocalSocket<IMessage> localRouterSocket,
                         ILocalSendingSocket<InternalRouteRegistration> internalRegistrationsSender,
                         ILocalSocketFactory localSocketFactory,
                         ILogger logger)
        {
            this.logger = logger;
            this.actorHandlerMap = actorHandlerMap;
            this.securityProvider = securityProvider;
            this.localRouterSocket = localRouterSocket;
            this.internalRegistrationsSender = internalRegistrationsSender;
            this.asyncQueue = asyncQueue;
            this.actorRegistrationsQueue = actorRegistrationsQueue;
            cancellationTokenSource = new CancellationTokenSource();
            receivingSocket = localSocketFactory.Create<IMessage>();
        }

        public void AssignActor(IActor actor)
        {
            var registrations = actorHandlerMap.Add(actor);
            if (registrations.Any())
            {
                actorRegistrationsQueue.Enqueue(registrations, cancellationTokenSource.Token);
            }
            else
            {
                logger.Warn($"Actor {actor.GetType().FullName} seems to not handle any message!");
            }
        }

        public bool CanAssignActor(IActor actor)
        {
            return actorHandlerMap.CanAdd(actor);
        }

        public void Start()
        {
            registrationsProcessing = Task.Factory.StartNew(_ => SafeExecute(() => RegisterActors(cancellationTokenSource.Token)),
                                                            cancellationTokenSource.Token,
                                                            TaskCreationOptions.LongRunning);
            syncProcessing = Task.Factory.StartNew(_ => SafeExecute(() => ProcessRequests(cancellationTokenSource.Token)),
                                                   cancellationTokenSource.Token,
                                                   TaskCreationOptions.LongRunning);
            asyncProcessing = Task.Factory.StartNew(_ => SafeExecute(() => ProcessAsyncResponses(cancellationTokenSource.Token)),
                                                    cancellationTokenSource.Token,
                                                    TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            registrationsProcessing?.Wait(TerminationWaitTimeout);
            syncProcessing?.Wait(TerminationWaitTimeout);
            asyncProcessing?.Wait(TerminationWaitTimeout);
        }

        private void RegisterActors(CancellationToken token)
        {
            try
            {
                foreach (var registrations in actorRegistrationsQueue.GetConsumingEnumerable(token))
                {
                    try
                    {
                        SendActorRegistrationMessage(registrations);
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

        private void SendActorRegistrationMessage(IEnumerable<ActorMessageHandlerIdentifier> registrations)
        {
            var registration = new InternalRouteRegistration
                               {
                                   MessageContracts = registrations.Select(mh => new MessageContract
                                                                                 {
                                                                                     Identifier = new MessageIdentifier(mh.Identifier.Identity,
                                                                                                                        mh.Identifier.Version,
                                                                                                                        mh.Identifier.Partition),
                                                                                     KeepRegistrationLocal = mh.KeepRegistrationLocal
                                                                                 })
                                                                   .ToArray(),
                                   DestinationSocket = receivingSocket
                               };

            internalRegistrationsSender.Send(registration);
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
                            messageOut.RegisterCallbackPoint(messageContext.CallbackReceiverIdentity,
                                                             messageContext.CallbackPoint,
                                                             messageContext.CallbackKey);
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

        private void ProcessRequests(CancellationToken token)
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

        private void HandleTaskResult(CancellationToken token, Task<IActorResult> task, Message messageIn)
        {
            if (task != null)
            {
                if (task.IsCompleted)
                {
                    var response = CreateTaskResultMessage(task).Messages;

                    MessageProcessed(messageIn, response);

                    foreach (var messageOut in response.Cast<Message>())
                    {
                        messageOut.RegisterCallbackPoint(messageIn.CallbackReceiverIdentity,
                                                         messageIn.CallbackPoint,
                                                         messageIn.CallbackKey);
                        messageOut.SetCorrelationId(messageIn.CorrelationId);
                        messageOut.CopyMessageRouting(messageIn.GetMessageRouting());
                        messageOut.TraceOptions |= messageIn.TraceOptions;

                        localRouterSocket.Send(messageOut);

                        ResponseSent(messageOut, true);
                    }
                }
                else
                {
                    task.ContinueWith(completed => SafeExecute(() => EnqueueTaskForCompletion(token, completed, messageIn)), token)
                        .ConfigureAwait(false);
                }
            }
        }

        private void CallbackException(Exception err, Message messageIn)
        {
            var messageOut = Message.Create(new ExceptionMessage
                                            {
                                                Exception = err,
                                                StackTrace = err.StackTrace
                                            },
                                            securityProvider.GetDomain(KinoMessages.Exception.Identity))
                                    .As<Message>();
            messageOut.RegisterCallbackPoint(messageIn.CallbackReceiverIdentity,
                                             messageIn.CallbackPoint,
                                             messageIn.CallbackKey);
            messageOut.SetCorrelationId(messageIn.CorrelationId);
            messageOut.CopyMessageRouting(messageIn.GetMessageRouting());
            messageOut.TraceOptions |= messageIn.TraceOptions;

            localRouterSocket.Send(messageOut);
        }

        private void EnqueueTaskForCompletion(CancellationToken token, Task<IActorResult> task, Message messageIn)
        {
            var asyncMessageContext = new AsyncMessageContext
                                      {
                                          OutMessages = CreateTaskResultMessage(task).Messages,
                                          CallbackPoint = messageIn.CallbackPoint,
                                          CallbackKey = messageIn.CallbackKey,
                                          CallbackReceiverIdentity = messageIn.CallbackReceiverIdentity,
                                          CorrelationId = messageIn.CorrelationId,
                                          MessageHops = messageIn.GetMessageRouting(),
                                          TraceOptions = messageIn.TraceOptions
                                      };
            asyncQueue.Enqueue(asyncMessageContext, token);
        }

        private IActorResult CreateTaskResultMessage(Task<IActorResult> task)
        {
            if (task.IsCanceled)
            {
                return new ActorResult(Message.Create(new ExceptionMessage
                                                      {
                                                          Exception = new OperationCanceledException()
                                                      },
                                                      securityProvider.GetDomain(KinoMessages.Exception.Identity)));
            }
            if (task.IsFaulted)
            {
                var err = task.Exception?.InnerException ?? task.Exception;

                return new ActorResult(Message.Create(new ExceptionMessage
                                                      {
                                                          Exception = err,
                                                          StackTrace = err?.StackTrace
                                                      },
                                                      securityProvider.GetDomain(KinoMessages.Exception.Identity)));
            }

            return task.Result ?? ActorResult.Empty;
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