using System;
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
        private Thread workingThread;
        private readonly CancellationTokenSource cancellationTokenSource;

        public ActorHost(NetMQContext context)
        {
            this.context = context;
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
            workingThread = new Thread(_ => ProcessRequests(cancellationTokenSource.Token));
            workingThread.Start();
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            workingThread.Join();
        }

        private void ProcessRequests(CancellationToken token)
        {
            try
            {
                using (var socket = CreateSocket(context))
                {
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
                            if(task != null)
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
                            }

                            //SignalWorkerReady(socket);
                        }
                        catch (Exception err)
                        {
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

        private NetMQSocket CreateSocket(NetMQContext context)
        {
            var socket = context.CreateDealerSocket();
            socket.Options.Identity = new byte[] {5, 5, 5};
            socket.Connect(endpointAddress);

            SignalWorkerReady(socket);

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
}