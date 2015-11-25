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
        private readonly IAsyncQueue<IActor> actorRegistrationsQueue;
        private readonly TaskCompletionSource<byte[]> localSocketIdentityPromise;
        private readonly ILogger logger;
        private static readonly TimeSpan TerminationWaitTimeout = TimeSpan.FromSeconds(3);

        public ActorHost(ISocketFactory socketFactory,
                         IActorHandlerMap actorHandlerMap,
                         IAsyncQueue<AsyncMessageContext> asyncQueue,
                         IAsyncQueue<IActor> actorRegistrationsQueue,
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
            actorRegistrationsQueue.Enqueue(actor, cancellationTokenSource.Token);
        }

        public void Start()
        {
            const int participantCount = 4;
            using (var gateway = new Barrier(participantCount))
            {
                registrationsProcessing = Task.Factory.StartNew(_ => SafeExecute(() => RegisterActors(cancellationTokenSource.Token, gateway)),
                                                                cancellationTokenSource.Token,
                                                                TaskCreationOptions.LongRunning);
                syncProcessing = Task.Factory.StartNew(_ => SafeExecute(() => ProcessRequests(cancellationTokenSource.Token, gateway)),
                                                       cancellationTokenSource.Token,
                                                       TaskCreationOptions.LongRunning);
                asyncProcessing = Task.Factory.StartNew(_ => SafeExecute(() => ProcessAsyncResponses(cancellationTokenSource.Token, gateway)),
                                                        cancellationTokenSource.Token,
                                                        TaskCreationOptions.LongRunning);

                gateway.SignalAndWait(cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            registrationsProcessing.Wait(TerminationWaitTimeout);
            syncProcessing.Wait(TerminationWaitTimeout);
            asyncProcessing.Wait(TerminationWaitTimeout);
        }

        private void RegisterActors(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var socket = CreateOneWaySocket())
                {
                    var localSocketIdentity = localSocketIdentityPromise.Task.Result;
                    gateway.SignalAndWait(token);

                    foreach (var actor in actorRegistrationsQueue.GetConsumingEnumerable(token))
                    {
                        try
                        {
                            var registrations = actorHandlerMap.Add(actor);
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

        private static void SendActorRegistrationMessage(ISocket socket, byte[] identity, IEnumerable<MessageIdentifier> registrations)
        {
            var payload = new RegisterInternalMessageRouteMessage
                          {
                              SocketIdentity = identity,
                              MessageContracts = registrations
                                  .Select(mh => new MessageContract
                                                {
                                                    Identity = mh.Identity,
                                                    Version = mh.Version
                                                })
                                  .ToArray()
                          };

            socket.SendMessage(Message.Create(payload));
        }

        private void ProcessAsyncResponses(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var localSocket = CreateOneWaySocket())
                {
                    gateway.SignalAndWait(token);

                    foreach (var messageContext in asyncQueue.GetConsumingEnumerable(token))
                    {
                        try
                        {
                            foreach (var messageOut in messageContext.OutMessages.Cast<Message>())
                            {
                                messageOut.RegisterCallbackPoint(messageContext.CallbackReceiverIdentity, messageContext.CallbackPoint);
                                messageOut.SetCorrelationId(messageContext.CorrelationId);
                                messageOut.CopyMessageHops(messageContext.MessageHops);
                                messageOut.TraceOptions = messageContext.TraceOptions;

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

        private void ProcessRequests(CancellationToken token, Barrier gateway)
        {
            using (var localSocket = CreateRoutableSocket())
            {
                localSocketIdentityPromise.SetResult(localSocket.GetIdentity());
                gateway.SignalAndWait(token);

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
            if (task.IsCompleted)
            {
                var response = CreateTaskResultMessage(task).Messages;

                MessageProcessed(messageIn, response.Count());

                foreach (var messageOut in response.Cast<Message>())
                {
                    messageOut.RegisterCallbackPoint(messageIn.CallbackReceiverIdentity, messageIn.CallbackPoint);
                    messageOut.SetCorrelationId(messageIn.CorrelationId);
                    messageOut.CopyMessageHops(messageIn.GetMessageHops());
                    messageOut.TraceOptions = messageIn.TraceOptions;

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

        private static void CallbackException(ISocket localSocket, Exception err, Message messageIn)
        {
            var messageOut = (Message) Message.Create(new ExceptionMessage {Exception = err});
            messageOut.RegisterCallbackPoint(messageIn.CallbackReceiverIdentity, messageIn.CallbackPoint);
            messageOut.SetCorrelationId(messageIn.CorrelationId);
            messageOut.CopyMessageHops(messageIn.GetMessageHops());
            messageOut.TraceOptions = messageIn.TraceOptions;

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
                                          MessageHops = messageIn.GetMessageHops(),
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
                                                          Exception = err
                                                      }));
            }

            return task.Result ?? new ActorResult(Enumerable.Empty<IMessage>().ToArray());
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