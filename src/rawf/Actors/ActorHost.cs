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

        public ActorHost(IConnectivityProvider connectivityProvider, IHostConfiguration config)
        {
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
                using (var localSocket = CreateSocket(context, null))
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

                                var response = new MultipartMessage(messageOut);
                                localSocket.SendMessage(new NetMQMessage(response.Frames));
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

        private void NotifyCommonExceptionHandler(NetMQSocket localSocket, Exception err, byte[] correlationId)
        {
            var message = (Message) Message.Create(new ExceptionMessage {Exception = err}, ExceptionMessage.MessageIdentity);
            message.SetCorrelationId(correlationId);
            var multipart = new MultipartMessage(message);
            localSocket.SendMessage(new NetMQMessage(multipart.Frames));
        }

        private void ProcessRequests(CancellationToken token)
        {
            try
            {
                using (var localSocket = CreateSocket(context, new byte[] {5, 5, 5}))
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

        private void HandleTaskResult(CancellationToken token, Task<IMessage> task, Message inMessage, NetMQSocket localSocket)
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

                        var response = new MultipartMessage(messageOut);
                        localSocket.SendMessage(new NetMQMessage(response.Frames));
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

        private void CallbackException(NetMQSocket localSocket, Exception err, MultipartMessage inMessage)
        {
            var message = (Message) Message.Create(new ExceptionMessage {Exception = err}, ExceptionMessage.MessageIdentity);
            message.RegisterCallbackPoint(ExceptionMessage.MessageIdentity, inMessage.GetCallbackReceiverIdentity());
            message.SetCorrelationId(inMessage.GetCorrelationId());
            var multipart = new MultipartMessage(message);

            localSocket.SendMessage(new NetMQMessage(multipart.Frames));
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

        private NetMQSocket CreateSocket(NetMQContext context, byte[] identity)
        {
            var socket = context.CreateDealerSocket();
            if (identity != null)
            {
                socket.Options.Identity = identity;
            }
            socket.Connect(endpointAddress);

            return socket;
        }

        private void RegisterActor(NetMQSocket socket)
        {
            var payload = new RegisterMessageHandlers
                          {
                              SocketIdentity = socket.Options.Identity,
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
            var multipartMessage = new MultipartMessage(Message.Create(payload, RegisterMessageHandlers.MessageIdentity));
            socket.SendMessage(new NetMQMessage(multipartMessage.Frames));
        }
    }
}