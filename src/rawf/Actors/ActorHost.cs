using System;
using System.Collections.Concurrent;
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
        private readonly string endpointAddress;
        private Task syncProcessing;
        private Task asyncProcessing;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly BlockingCollection<AsyncMessageContext> asyncResponses;
        private readonly IConnectivityProvider connectivityProvider;

        public ActorHost(IActorHandlersMap actorHandlersMap, IConnectivityProvider connectivityProvider, IHostConfiguration config)
        {
            this.actorHandlersMap = actorHandlersMap;
            this.connectivityProvider = connectivityProvider;
            endpointAddress = config.GetRouterAddress();
            asyncResponses = new BlockingCollection<AsyncMessageContext>(new ConcurrentQueue<AsyncMessageContext>());
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void AssignActor(IActor actor)
        {
            actorHandlersMap.Add(actor);
        }

        public void Start()
        {
            AssertActorIsAssigned();

            syncProcessing = Task.Factory.StartNew(_ => ProcessRequests(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);
            asyncProcessing = Task.Factory.StartNew(_ => ProcessAsyncResponses(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        private void AssertActorIsAssigned()
        {
            if (actorHandlersMap == null)
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
                asyncResponses.Dispose();
            }
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
                                var messageIn = new Message(multipart);
                                var actorIdentifier = new ActorIdentifier(messageIn.Version, messageIn.Identity);
                                var handler = actorHandlersMap.Get(actorIdentifier);

                                var task = handler(messageIn);

                                HandleTaskResult(token, task, messageIn, localSocket);
                            }
                            catch (Exception err)
                            {
                                //TODO: Add more context to exception about which Actor failed
                                CallbackException(localSocket, err, multipart);
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
                                              CallbackIdentity = GetTaskCallbackIdentity(task, messageIn),
                                              CallbackReceiverIdentity = messageIn.CallbackReceiverIdentity,
                                              CorrelationId = messageIn.CorrelationId
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
                              Registrations = actorHandlersMap
                                  .GetRegisteredIdentifiers()
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