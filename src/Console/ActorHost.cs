using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Console.Messages;
using NetMQ;

namespace Console
{
    public class ActorHost : IActorHost
    {
        private IDictionary<ActorIdentifier, MessageHandler> messageHandlers;
        private readonly NetMQContext context;
        private const string endpointAddress = Program.EndpointAddress;
        private Task syncProcessingTask;
        private Task asyncProcessingTask;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly BlockingCollection<AsyncMessageContext> asyncResponses;

        public ActorHost(NetMQContext context)
        {
            this.context = context;
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
                .ToDictionary(d => new ActorIdentifier(d.Message.Version.GetBytes(),
                                                       d.Message.Identity.GetBytes()),
                              d => d.Handler);
        }

        public void Start()
        {
            syncProcessingTask = Task.Factory.StartNew(_ => ProcessRequests(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);
            asyncProcessingTask = Task.Factory.StartNew(_ => ProcessAsyncResponses(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            syncProcessingTask.Wait();
            asyncProcessingTask.Wait();
        }

        private void ProcessAsyncResponses(CancellationToken token)
        {
            try
            {
                using (var socket = CreateSocket(context, null))
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

                                var response = new MultipartMessage(messageOut, socket.Options.Identity);
                                socket.SendMessage(new NetMQMessage(response.Frames));
                            }
                        }
                        catch (Exception err)
                        {
                            //TODO: Send error message
                            System.Console.WriteLine(err);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception err)
            {
                System.Console.WriteLine(err);
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
                using (var socket = CreateSocket(context, new byte[] {5, 5, 5}))
                {
                    SignalWorkerReady(socket);

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var request = socket.ReceiveMessage();
                            var multipart = new MultipartMessage(request);
                            var messageIn = new Message(multipart);
                            var handler = messageHandlers[new ActorIdentifier(multipart.GetMessageVersion(),
                                                                              multipart.GetMessageIdentity())];

                            var task = handler(messageIn);
                            if (task != null)
                            {
                                //TODO: Implement logic for IsCanceled or IsFalted
                                if (task.IsCompleted)
                                {
                                    var messageOut = (Message) task.Result;
                                    if (messageOut != null)
                                    {
                                        messageOut.RegisterCallbackPoint(messageIn.CallbackIdentity, messageIn.CallbackReceiverIdentity);
                                        messageOut.SetCorrelationId(messageIn.CorrelationId);

                                        var response = new MultipartMessage(messageOut, socket.Options.Identity);
                                        socket.SendMessage(new NetMQMessage(response.Frames));
                                    }
                                }
                                else
                                {
                                    task.ContinueWith(completed => EnqueueTaskForCompletion(token, completed, messageIn), token)
                                        .ConfigureAwait(false);
                                }
                            }

                            //SignalWorkerReady(socket);
                        }
                        catch (Exception err)
                        {
                            //TODO: Send error message
                            System.Console.WriteLine(err);
                        }
                    }
                }
            }
            catch (Exception err)
            {
                System.Console.WriteLine(err);
            }
        }

        private void EnqueueTaskForCompletion(CancellationToken token, Task<IMessage> task, IMessage messageIn)
        {
            try
            {
                var asyncMessageContext = new AsyncMessageContext
                                          {
                                              OutMessage = task.Result,
                                              CorrelationId = messageIn.CorrelationId,
                                              CallbackIdentity = messageIn.CallbackIdentity,
                                              CallbackReceiverIdentity = messageIn.CallbackReceiverIdentity
                                          };
                asyncResponses.Add(asyncMessageContext, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception err)
            {
                System.Console.WriteLine(err);
            }
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

        private void SignalWorkerReady(NetMQSocket socket)
        {
            var payload = new RegisterMessageHandlers
                          {
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
            var multipartMessage = new MultipartMessage(Message.Create(payload, RegisterMessageHandlers.MessageIdentity), socket.Options.Identity);
            socket.SendMessage(new NetMQMessage(multipartMessage.Frames));
        }
    }

    internal class AsyncMessageContext
    {
        internal IMessage OutMessage { get; set; }
        internal byte[] CorrelationId { get; set; }
        internal byte[] CallbackIdentity { get; set; }
        internal byte[] CallbackReceiverIdentity { get; set; }
    }
}