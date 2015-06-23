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
        private IDictionary<string, MessageHandler> messageHandlers;
        private readonly NetMQContext context;
        private const string endpointAddress = "inproc://local";
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
            this.actor = actor;
        }

        private static IDictionary<string, MessageHandler> BuildMessageHandlersMap(IActor actor)
        {
            return actor.GetInterfaceDefinition().ToDictionary(d => d.Message.Type, d => d.Handler);
        }

        public void Start()
        {
            workingThread = new Thread(_ => StartWorkerHost(cancellationTokenSource.Token));
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
                            var handler = messageHandlers[messageIn.Identity];

                            var messageOut = (Message) handler(messageIn);

                            if (messageOut != null)
                            {
                                messageOut = messageOut
                                    .RegisterEndOfFlowReceiver(messageIn.EndOfFlowIdentity, messageIn.EndOfFlowReceiverIdentity)
                                    .SetCorrelationId(messageIn.CorrelationId);

                                var response = new MultipartMessage(messageOut, socket.Options.Identity);
                                socket.SendMessage(new NetMQMessage(response.Frames));
                            }

                            SignalWorkerReady(socket);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private NetMQSocket CreateSocket(NetMQContext context)
        {
            var socket = context.CreateDealerSocket();
            socket.Options.RouterMandatory = true;
            socket.Options.Identity = Guid.NewGuid().ToByteArray();
            socket.Connect(endpointAddress);

            return socket;
        }

        private void SignalWorkerReady(NetMQSocket socket)
        {
            var payload = new WorkerReady {MessageIdentities = messageHandlers.Keys};
            var multipartMessage = new MultipartMessage(Message.Create(payload, WorkerReady.MessageIdentity), socket.Options.Identity);
            socket.SendMessage(new NetMQMessage(multipartMessage.Frames));
        }
    }
}