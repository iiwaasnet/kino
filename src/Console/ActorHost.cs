using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Console.Messages;
using NetMQ;

namespace Console
{
    public class ActorHost : IActorHost
    {
        private IActor actor;
        private IDictionary<MessageIdentifier, MessageHandler> messageHandlers;
        private readonly NetMQContext context;
        private const string endpointAddress = Program.EndpointAddress;
        private Thread workingThread;
        private readonly CancellationTokenSource cancellationTokenSource;
        private static readonly byte[] AnyReceiver = new byte[0];

        public ActorHost(NetMQContext context)
        {
            this.context = context;
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void AssignActor(IActor actor)
        {
            messageHandlers = BuildMessageHandlersMap(actor);
            this.actor = actor;
        }

        private static IDictionary<MessageIdentifier, MessageHandler> BuildMessageHandlersMap(IActor actor)
        {
            return actor
                .GetInterfaceDefinition()
                .ToDictionary(d => new MessageIdentifier(d.Message.Version.GetBytes(),
                                                         d.Message.Identity.GetBytes(),
                                                         AnyReceiver), d => d.Handler);
        }

        public void Start()
        {
            workingThread = new Thread(_ => StartWorkerHost(cancellationTokenSource.Token));
            workingThread.Start();
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            workingThread.Join();
        }

        private void StartWorkerHost(CancellationToken token)
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
                            var handler = messageHandlers[new MessageIdentifier(multipart.GetMessageVersion(),
                                                                                multipart.GetMessageIdentity(),
                                                                                multipart.GetReceiverIdentity())];

                            var messageOut = (Message) handler(messageIn);

                            if (messageOut != null)
                            {
                                messageOut.RegisterCallbackPoint(messageIn.CallbackIdentity, messageIn.CallbackReceiverIdentity);
                                messageOut.SetCorrelationId(messageIn.CorrelationId);

                                var response = new MultipartMessage(messageOut, socket.Options.Identity);
                                socket.SendMessage(new NetMQMessage(response.Frames));
                            }

                            SignalWorkerReady(socket);
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
            //socket.Options.RouterMandatory = true;
            socket.Options.Identity = Guid.NewGuid().ToString().GetBytes();
            socket.Options.Identity = new byte[] {5, 5, 5};
            socket.Connect(endpointAddress);

            SignalWorkerReady(socket);

            return socket;
        }

        private void SignalWorkerReady(NetMQSocket socket)
        {
            var payload = new WorkerReady
                          {
                              MessageIdentities = messageHandlers
                                  .Keys
                                  .Select(mh => new MessageIdentity
                                                {
                                                    Identity = mh.MessageIdentity.GetString(),
                                                    Version = mh.Version.GetString(),
                                                    ReceiverIdentity = mh.ReceiverIdentity.GetString()
                                                })
                          };
            var multipartMessage = new MultipartMessage(Message.Create(payload, WorkerReady.MessageIdentity), socket.Options.Identity);
            socket.SendMessage(new NetMQMessage(multipartMessage.Frames));
        }
    }
}