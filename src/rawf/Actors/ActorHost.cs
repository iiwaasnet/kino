using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using rawf.Connectivity;
using rawf.Messaging;
using rawf.Messaging.Messages;
using rawf.Sockets;

namespace rawf.Actors
{
    public class ActorHost : IActorHost
    {
        private IDictionary<ActorIdentifier, MessageHandler> messageHandlers;
        private readonly NetMQContext context;
        private readonly string endpointAddress;
        private Task syncProcessing;
        private Task asyncProcessing;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly BlockingCollection<AsyncMessageContext> asyncResponses;
        private readonly IConnectivityProvider connectivityProvider;

        public ActorHost(IConnectivityProvider connectivityProvider, IHostConfiguration config)
        {
            this.connectivityProvider = connectivityProvider;
            context = connectivityProvider.GetConnectivityContext();
            endpointAddress = config.GetRouterAddress();
            asyncResponses = new BlockingCollection<AsyncMessageContext>(new ConcurrentQueue<AsyncMessageContext>());
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void AssignActor(IActor actor)
        {
            messageHandlers = BuildMessageHandlersMap(actor);
        }

        private static IDictionary<ActorIdentifier, MessageHandler> BuildMessageHandlersMap(IActor actor)
        {
            return actor
                .GetInterfaceDefinition()
                .ToDictionary(d => new ActorIdentifier(d.Message.Version,
                                                       d.Message.Identity),
                              d => d.Handler);
        }

        public void Start()
        {
            AssertActorIsAssigned();

            syncProcessing = Task.Factory.StartNew(_ => ProcessRequests(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);
            asyncProcessing = Task.Factory.StartNew(_ => ProcessAsyncResponses(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        private void AssertActorIsAssigned()
        {
            if (messageHandlers == null)
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

        private void ProcessAsyncResponses(CancellationToken token)
        {
            try
            {
                using (var localSocket = CreateSocket(null))
                {
                    foreach (var messageContext in asyncResponses.GetConsumingEnumerable(token))
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
                            NotifyCommonExceptionHandler(localSocket, err, CorrelationId.Infrastructural);
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
                asyncResponses.Dispose();
            }
        }

        private void NotifyCommonExceptionHandler(ISocket localSocket, Exception err, byte[] correlationId)
        {
            var message = (Message) Message.Create(new ExceptionMessage {Exception = err}, ExceptionMessage.MessageIdentity);
            message.SetCorrelationId(correlationId);
            localSocket.SendMessage(message);
        }

        private void ProcessRequests(CancellationToken token)
        {
            try
            {
                using (var localSocket = CreateSocket(new byte[] {5, 5, 5}))
                {
                    RegisterActor(localSocket);

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var request = localSocket.ReceiveMessage();
                            var multipart = new MultipartMessage(request);

                            try
                            {
                                var inMessage = new Message(multipart);
                                var handler = messageHandlers[new ActorIdentifier(multipart.GetMessageVersion(),
                                                                                  multipart.GetMessageIdentity())];

                                var task = handler(inMessage);

                                HandleTaskResult(token, task, inMessage, localSocket);
                            }
                            catch (Exception err)
                            {
                                //TODO: Add more context to exception about which Actor failed
                                CallbackException(localSocket, err, multipart);
                            }
                        }
                        catch (Exception err)
                        {
                            NotifyCommonExceptionHandler(localSocket, err, CorrelationId.Infrastructural);
                        }
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }

        private void HandleTaskResult(CancellationToken token, Task<IMessage> task, Message inMessage, ISocket localSocket)
        {
            switch (task.Status)
            {
                case TaskStatus.RanToCompletion:
                case TaskStatus.Faulted:
                    var messageOut = (Message) task.Result;
                    if (messageOut != null)
                    {
                        messageOut.RegisterCallbackPoint(inMessage.CallbackIdentity, inMessage.CallbackReceiverIdentity);
                        messageOut.SetCorrelationId(inMessage.CorrelationId);

                        localSocket.SendMessage(messageOut);
                    }
                    break;
                case TaskStatus.Canceled:
                    throw new OperationCanceledException();
                case TaskStatus.Created:
                case TaskStatus.Running:
                case TaskStatus.WaitingForActivation:
                case TaskStatus.WaitingForChildrenToComplete:
                case TaskStatus.WaitingToRun:
                    task.ContinueWith(completed => EnqueueTaskForCompletion(token, completed, inMessage), token)
                        .ConfigureAwait(false);
                    break;
                default:
                    throw new ThreadStateException($"TaskStatus: {task.Status}");
            }
        }

        private void CallbackException(ISocket localSocket, Exception err, MultipartMessage inMessage)
        {
            var message = (Message) Message.Create(new ExceptionMessage {Exception = err}, ExceptionMessage.MessageIdentity);
            message.RegisterCallbackPoint(ExceptionMessage.MessageIdentity, inMessage.GetCallbackReceiverIdentity());
            message.SetCorrelationId(inMessage.GetCorrelationId());

            localSocket.SendMessage(message);
        }

        private void EnqueueTaskForCompletion(CancellationToken token, Task<IMessage> task, IMessage messageIn)
        {
            try
            {
                var asyncMessageContext = new AsyncMessageContext
                                          {
                                              OutMessage = CreateTaskResultMessage(task),
                                              CorrelationId = messageIn.CorrelationId,
                                              CallbackIdentity = GetTaskCallbackIdentity(task, messageIn),
                                              CallbackReceiverIdentity = messageIn.CallbackReceiverIdentity
                                          };
                asyncResponses.Add(asyncMessageContext, token);
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
                                      }, ExceptionMessage.MessageIdentity);
            }
            if (task.IsFaulted)
            {
                return Message.Create(new ExceptionMessage
                                      {
                                          Exception = task.Exception
                                      }, ExceptionMessage.MessageIdentity);
            }

            return task.Result;
        }

        private ISocket CreateSocket(byte[] identity)
        {
            var socket = connectivityProvider.CreateDealerSocket();
            if (identity != null)
            {
                socket.SetIdentity(identity);
            }
            socket.Connect(endpointAddress);

            return socket;
        }

        private void RegisterActor(ISocket socket)
        {
            var payload = new RegisterMessageHandlers
                          {
                              SocketIdentity = socket.GetIdentity(),
                              Registrations = messageHandlers
                                  .Keys
                                  .Select(mh => new MessageHandlerRegistration
                                                {
                                                    Identity = mh.Identity,
                                                    Version = mh.Version,
                                                    IdentityType = IdentityType.Actor
                                                })
                                  .ToArray()
                          };

            socket.SendMessage(Message.Create(payload, RegisterMessageHandlers.MessageIdentity));
        }
    }
}