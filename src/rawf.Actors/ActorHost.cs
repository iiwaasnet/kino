using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using rawf.Connectivity;
using rawf.Messaging;
using rawf.Messaging.Messages;
using rawf.Sockets;

namespace rawf.Actors
{
    public class ActorHost : IActorHost
    {
        private readonly IActorHandlersMap actorHandlersMap;
        private Task syncProcessing;
        private Task asyncProcessing;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly IMessagesCompletionQueue messagesCompletionQueue;
        private readonly ISocketFactory socketFactory;
        private readonly IRouterConfiguration routerConfiguration;

        public ActorHost(ISocketFactory socketFactory,
                         IActorHandlersMap actorHandlersMap,
                         IMessagesCompletionQueue messagesCompletionQueue,
                         IRouterConfiguration routerConfiguration)
        {
            this.actorHandlersMap = actorHandlersMap;
            this.socketFactory = socketFactory;
            this.routerConfiguration = routerConfiguration;
            this.messagesCompletionQueue = messagesCompletionQueue;
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void AssignActor(IActor actor)
        {
            actorHandlersMap.Add(actor);
        }

        public void Start()
        {
            AssertActorIsAssigned();

            const int participantCount = 3;
            using (var gateway = new Barrier(participantCount))
            {
                syncProcessing = Task.Factory.StartNew(_ => ProcessRequests(cancellationTokenSource.Token, gateway),
                                                       TaskCreationOptions.LongRunning);
                asyncProcessing = Task.Factory.StartNew(_ => ProcessAsyncResponses(cancellationTokenSource.Token, gateway),
                                                        TaskCreationOptions.LongRunning);

                gateway.SignalAndWait(cancellationTokenSource.Token);
            }
        }

        private void AssertActorIsAssigned()
        {
            if (!actorHandlersMap.GetMessageHandlerIdentifiers().Any())
            {
                throw new Exception("Actor is not assigned!");
            }
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            syncProcessing.Wait();
            asyncProcessing.Wait();
        }

        private void ProcessAsyncResponses(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var localSocket = CreateOneWaySocket())
                {
                    gateway.SignalAndWait(token);

                    foreach (var messageContext in messagesCompletionQueue.GetMessages(token))
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
                messagesCompletionQueue.Dispose();
            }
        }

        private void ProcessRequests(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var localSocket = CreateRoutableSocket())
                {
                    RegisterActor(localSocket);

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
                                    var handler = actorHandlersMap.Get(actorIdentifier);

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

        private void CallbackException(ISocket localSocket, Exception err, IMessage messageIn)
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
                messagesCompletionQueue.Enqueue(asyncMessageContext, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }

        private static byte[] GetTaskCallbackIdentity(Task<IMessage> task, IMessage messageIn)
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

        private void RegisterActor(ISocket socket)
        {
            var payload = new RegisterMessageHandlersMessage
                          {
                              SocketIdentity = socket.GetIdentity(),
                              MessageHandlers = actorHandlersMap
                                  .GetMessageHandlerIdentifiers()
                                  .Select(mh => new MessageHandlerRegistration
                                                {
                                                    Identity = mh.Identity,
                                                    Version = mh.Version
                                                })
                                  .ToArray()
                          };

            socket.SendMessage(Message.Create(payload, RegisterMessageHandlersMessage.MessageIdentity));
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