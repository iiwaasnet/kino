using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using rawf.Connectivity;
using rawf.Framework;
using rawf.Messaging;
using rawf.Messaging.Messages;
using rawf.Sockets;

namespace rawf.Actors
{
    public class ActorHost : IActorHost
    {
        private readonly IActorHandlerMap actorHandlerMap;
        private Task syncProcessing;
        private Task asyncProcessing;
        private Task registrationsProcessing;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly IAsyncQueue<AsyncMessageContext> asyncQueue;
        private readonly ISocketFactory socketFactory;
        private readonly IRouterConfiguration routerConfiguration;
        private readonly IAsyncQueue<IActor> actorRegistrationsQueue;
        private readonly TaskCompletionSource<byte[]> localSocketIdentityPromise;

        public ActorHost(ISocketFactory socketFactory,
                         IActorHandlerMap actorHandlerMap,
                         IAsyncQueue<AsyncMessageContext> asyncQueue,
                         IAsyncQueue<IActor> actorRegistrationsQueue,
                         IRouterConfiguration routerConfiguration)
        {
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
                registrationsProcessing = Task.Factory.StartNew(_ => RegisterActors(cancellationTokenSource.Token, gateway),
                                                                cancellationTokenSource.Token,
                                                                TaskCreationOptions.LongRunning);
                syncProcessing = Task.Factory.StartNew(_ => ProcessRequests(cancellationTokenSource.Token, gateway),
                                                       cancellationTokenSource.Token,
                                                       TaskCreationOptions.LongRunning);
                asyncProcessing = Task.Factory.StartNew(_ => ProcessAsyncResponses(cancellationTokenSource.Token, gateway),
                                                        cancellationTokenSource.Token,
                                                        TaskCreationOptions.LongRunning);

                gateway.SignalAndWait(cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            registrationsProcessing.Wait();
            syncProcessing.Wait();
            asyncProcessing.Wait();
        }

        private void SafeExecute(LambdaExpression wrappedMethod)
        {
            var method = wrappedMethod.Compile().DynamicInvoke();
        }

        private void RegisterActors(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var socket = CreateOneWaySocket())
                {
                    var localSocketIdentity = localSocketIdentityPromise.Task.Result;
                    gateway.SignalAndWait(token);

                    foreach (var actor in actorRegistrationsQueue.GetMessages(token))
                    {
                        try
                        {
                            var registrations = actorHandlerMap.Add(actor);
                            SendActorRegistrationMessage(socket, localSocketIdentity, registrations);
                        }
                        catch (Exception err)
                        {
                            Console.WriteLine(err);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
            finally
            {
                actorRegistrationsQueue.Dispose();
            }
        }

        private static void SendActorRegistrationMessage(ISocket socket, byte[] identity, IEnumerable<MessageHandlerIdentifier> registrations)
        {
            var payload = new RegisterMessageHandlersMessage
                          {
                              SocketIdentity = identity,
                              MessageHandlers = registrations
                                  .Select(mh => new MessageHandlerRegistration
                                                {
                                                    Identity = mh.Identity,
                                                    Version = mh.Version
                                                })
                                  .ToArray()
                          };

            socket.SendMessage(Message.Create(payload, RegisterMessageHandlersMessage.MessageIdentity));
        }

        private void ProcessAsyncResponses(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var localSocket = CreateOneWaySocket())
                {
                    gateway.SignalAndWait(token);

                    foreach (var messageContext in asyncQueue.GetMessages(token))
                    {
                        try
                        {
                            var messageOut = (Message) messageContext.OutMessage;
                            if (messageOut != null)
                            {
                                messageOut.RegisterCallbackPoint(messageContext.CallbackIdentity, messageContext.CallbackReceiverIdentity);
                                messageOut.SetCorrelationId(messageContext.CorrelationId);

                                localSocket.SendMessage(messageOut);
                            }
                        }
                        catch (Exception err)
                        {
                            Console.WriteLine(err);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
            finally
            {
                asyncQueue.Dispose();
            }
        }

        private void ProcessRequests(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var localSocket = CreateRoutableSocket())
                {
                    localSocketIdentityPromise.SetResult(localSocket.GetIdentity());
                    gateway.SignalAndWait(token);

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var message = localSocket.ReceiveMessage(token);
                            if (message != null)
                            {
                                try
                                {
                                    var actorIdentifier = new MessageHandlerIdentifier(message.Version, message.Identity);
                                    var handler = actorHandlerMap.Get(actorIdentifier);

                                    var task = handler(message);

                                    HandleTaskResult(token, task, message, localSocket);
                                }
                                catch (Exception err)
                                {
                                    //TODO: Add more context to exception about which Actor failed
                                    CallbackException(localSocket, err, message);
                                }
                            }
                        }
                        catch (Exception err)
                        {
                            //TODO: Replace with proper logging
                            Console.WriteLine(err);
                        }
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }

        private void HandleTaskResult(CancellationToken token, Task<IMessage> task, IMessage messageIn, ISocket localSocket)
        {
            if (task.IsCompleted)
            {
                var messageOut = (Message) CreateTaskResultMessage(task);
                messageOut.RegisterCallbackPoint(GetTaskCallbackIdentity(task, messageIn), messageIn.CallbackReceiverIdentity);
                messageOut.SetCorrelationId(messageIn.CorrelationId);

                localSocket.SendMessage(messageOut);
            }
            else
            {
                task.ContinueWith(completed => EnqueueTaskForCompletion(token, completed, messageIn), token)
                    .ConfigureAwait(false);
            }
        }

        private static void CallbackException(ISocket localSocket, Exception err, IMessage messageIn)
        {
            var messageOut = (Message) Message.Create(new ExceptionMessage {Exception = err}, ExceptionMessage.MessageIdentity);
            messageOut.RegisterCallbackPoint(ExceptionMessage.MessageIdentity, messageIn.CallbackReceiverIdentity);
            messageOut.SetCorrelationId(messageIn.CorrelationId);

            localSocket.SendMessage(messageOut);
        }

        private void EnqueueTaskForCompletion(CancellationToken token, Task<IMessage> task, IMessage messageIn)
        {
            try
            {
                var asyncMessageContext = new AsyncMessageContext
                                          {
                                              OutMessage = CreateTaskResultMessage(task),
                                              CallbackIdentity = GetTaskCallbackIdentity(task, messageIn),
                                              CallbackReceiverIdentity = messageIn.CallbackReceiverIdentity,
                                              CorrelationId = messageIn.CorrelationId
                                          };
                asyncQueue.Enqueue(asyncMessageContext, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }

        private static byte[] GetTaskCallbackIdentity(Task task, IMessage messageIn)
        {
            return task.IsCanceled || task.IsFaulted
                       ? ExceptionMessage.MessageIdentity
                       : messageIn.CallbackIdentity;
        }

        private static IMessage CreateTaskResultMessage(Task<IMessage> task)
        {
            if (task.IsCanceled)
            {
                return Message.Create(new ExceptionMessage
                                      {
                                          Exception = new OperationCanceledException()
                                      },
                                      ExceptionMessage.MessageIdentity);
            }
            if (task.IsFaulted)
            {
                var err = task.Exception?.InnerException ?? task.Exception;

                return Message.Create(new ExceptionMessage
                                      {
                                          Exception = err
                                      },
                                      ExceptionMessage.MessageIdentity);
            }

            return task.Result;
        }

        private ISocket CreateOneWaySocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(routerConfiguration.RouterAddress.Uri);

            return socket;
        }

        private ISocket CreateRoutableSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.SetIdentity(SocketIdentifier.CreateNew());
            socket.Connect(routerConfiguration.RouterAddress.Uri);

            return socket;
        }
    }
}