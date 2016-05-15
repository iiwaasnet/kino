using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Sockets;

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
        private readonly ISocketFactory socketFactory;
        private readonly RouterConfiguration routerConfiguration;
        private readonly IAsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>> actorRegistrationsQueue;
        private readonly TaskCompletionSource<byte[]> localSocketIdentityPromise;
        private readonly ILogger logger;
        private static readonly TimeSpan TerminationWaitTimeout = TimeSpan.FromSeconds(3);

        public ActorHost(ISocketFactory socketFactory,
                         IActorHandlerMap actorHandlerMap,
                         IAsyncQueue<AsyncMessageContext> asyncQueue,
                         IAsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>> actorRegistrationsQueue,
                         RouterConfiguration routerConfiguration,
                         ILogger logger)
        {
            this.logger = logger;
            this.actorHandlerMap = actorHandlerMap;
            localSocketIdentityPromise = new TaskCompletionSource<byte[]>();
            this.socketFactory = socketFactory;
            this.routerConfiguration = routerConfiguration;
            this.asyncQueue = asyncQueue;
            this.actorRegistrationsQueue = actorRegistrationsQueue;
            cancellationTokenSource = new CancellationTokenSource();
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
                using (var socket = CreateOneWaySocket())
                {
                    var localSocketIdentity = localSocketIdentityPromise.Task.Result;

                    foreach (var registrations in actorRegistrationsQueue.GetConsumingEnumerable(token))
                    {
                        try
                        {
                            SendActorRegistrationMessage(socket, localSocketIdentity, registrations);
                        }
                        catch (Exception err)
                        {
                            logger.Error(err);
                        }
                    }
                }
            }
            finally
            {
                actorRegistrationsQueue.Dispose();
            }
        }

        private static void SendActorRegistrationMessage(ISocket socket, byte[] identity, IEnumerable<ActorMessageHandlerIdentifier> registrations)
        {
            var payload = new RegisterInternalMessageRouteMessage
                          {
                              SocketIdentity = identity,
                              LocalMessageContracts = registrations.Where(mh => mh.KeepRegistrationLocal)
                                                                   .Select(mh => new MessageContract
                                                                                 {
                                                                                     Identity = mh.Identifier.Identity,
                                                                                     Version = mh.Identifier.Version
                                                                                 })
                                                                   .ToArray(),
                              GlobalMessageContracts = registrations.Where(mh => !mh.KeepRegistrationLocal)
                                                                    .Select(mh => new MessageContract
                                                                                  {
                                                                                      Identity = mh.Identifier.Identity,
                                                                                      Version = mh.Identifier.Version
                                                                                  })
                                                                    .ToArray()
                          };

            socket.SendMessage(Message.Create(payload));
        }

        private void ProcessAsyncResponses(CancellationToken token)
        {
            try
            {
                using (var localSocket = CreateOneWaySocket())
                {
                    foreach (var messageContext in asyncQueue.GetConsumingEnumerable(token))
                    {
                        try
                        {
                            foreach (var messageOut in messageContext.OutMessages.Cast<Message>())
                            {
                                messageOut.RegisterCallbackPoint(messageContext.CallbackReceiverIdentity, messageContext.CallbackPoint);
                                messageOut.SetCorrelationId(messageContext.CorrelationId);
                                messageOut.CopyMessageRouting(messageContext.MessageHops);
                                messageOut.TraceOptions |= messageContext.TraceOptions;

                                localSocket.SendMessage(messageOut);

                                ResponseSent(messageOut, false);
                            }
                        }
                        catch (Exception err)
                        {
                            logger.Error(err);
                        }
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
            using (var localSocket = CreateRoutableSocket())
            {
                localSocketIdentityPromise.SetResult(localSocket.GetIdentity());

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var message = (Message) localSocket.ReceiveMessage(token);
                        if (message != null)
                        {
                            try
                            {
                                var actorIdentifier = new MessageIdentifier(message.Version, message.Identity);
                                var handler = actorHandlerMap.Get(actorIdentifier);
                                if (handler != null)
                                {
                                    var task = handler(message);

                                    HandleTaskResult(token, task, message, localSocket);
                                }
                                else
                                {
                                    HandlerNotFound(message);
                                }
                            }
                            catch (Exception err)
                            {
                                //TODO: Add more context to exception about which Actor failed
                                CallbackException(localSocket, err, message);
                                logger.Error(err);
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

        private void HandleTaskResult(CancellationToken token, Task<IActorResult> task, Message messageIn, ISocket localSocket)
        {
            if (task != null)
            {
                if (task.IsCompleted)
                {
                    var response = CreateTaskResultMessage(task).Messages;

                    MessageProcessed(messageIn, response);

                    foreach (var messageOut in response.Cast<Message>())
                    {
                        messageOut.RegisterCallbackPoint(messageIn.CallbackReceiverIdentity, messageIn.CallbackPoint);
                        messageOut.SetCorrelationId(messageIn.CorrelationId);
                        messageOut.CopyMessageRouting(messageIn.GetMessageRouting());
                        messageOut.TraceOptions |= messageIn.TraceOptions;

                        localSocket.SendMessage(messageOut);

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

        private static void CallbackException(ISocket localSocket, Exception err, Message messageIn)
        {
            var messageOut = (Message) Message.Create(new ExceptionMessage {Exception = err, StackTrace = err.StackTrace});
            messageOut.RegisterCallbackPoint(messageIn.CallbackReceiverIdentity, messageIn.CallbackPoint);
            messageOut.SetCorrelationId(messageIn.CorrelationId);
            messageOut.CopyMessageRouting(messageIn.GetMessageRouting());
            messageOut.TraceOptions |= messageIn.TraceOptions;

            localSocket.SendMessage(messageOut);
        }

        private void EnqueueTaskForCompletion(CancellationToken token, Task<IActorResult> task, Message messageIn)
        {
            var asyncMessageContext = new AsyncMessageContext
                                      {
                                          OutMessages = CreateTaskResultMessage(task).Messages,
                                          CallbackPoint = messageIn.CallbackPoint,
                                          CallbackReceiverIdentity = messageIn.CallbackReceiverIdentity,
                                          CorrelationId = messageIn.CorrelationId,
                                          MessageHops = messageIn.GetMessageRouting(),
                                          TraceOptions = messageIn.TraceOptions
                                      };
            asyncQueue.Enqueue(asyncMessageContext, token);
        }

        private static IActorResult CreateTaskResultMessage(Task<IActorResult> task)
        {
            if (task.IsCanceled)
            {
                return new ActorResult(Message.Create(new ExceptionMessage
                                                      {
                                                          Exception = new OperationCanceledException()
                                                      }));
            }
            if (task.IsFaulted)
            {
                var err = task.Exception?.InnerException ?? task.Exception;

                return new ActorResult(Message.Create(new ExceptionMessage
                                                      {
                                                          Exception = err,
                                                          StackTrace = err.StackTrace
                                                      }));
            }

            return task.Result ?? ActorResult.Empty;
        }

        private ISocket CreateOneWaySocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            SocketHelper.SafeConnect(() => socket.Connect(routerConfiguration.RouterAddress.Uri));

            return socket;
        }

        private ISocket CreateRoutableSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.SetIdentity(SocketIdentifier.CreateIdentity());
            SocketHelper.SafeConnect(() => socket.Connect(routerConfiguration.RouterAddress.Uri));

            return socket;
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