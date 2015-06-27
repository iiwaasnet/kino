using System;
using System.Threading;
using Console.Messages;
using Framework;
using NetMQ;

namespace Console
{
    public class MessageRouter : IMessageRouter
    {
        private const string localEndpointAddress = Program.EndpointAddress;
        private readonly CancellationTokenSource cancellationTokenSource;
        private Thread workingThread;
        private readonly MessageHandlerStack messageHandlers;
        private readonly NetMQContext context;

        public MessageRouter(NetMQContext context)
        {
            this.context = context;
            messageHandlers = new MessageHandlerStack();
            cancellationTokenSource = new CancellationTokenSource();
        }

        private NetMQSocket CreateSocket(NetMQContext context)
        {
            var socket = context.CreateRouterSocket();
            socket.Options.RouterMandatory = true;
            socket.Bind(localEndpointAddress);

            return socket;
        }

        public void Start()
        {
            workingThread = new Thread(_ => MessageProcessingLoop(cancellationTokenSource.Token));
            workingThread.Start();
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            workingThread.Join();
        }

        private void MessageProcessingLoop(CancellationToken token)
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
                            var multipart = new MultipartMessage(request, true);


                            if (Unsafe.Equals(multipart.GetMessageIdentity(), WorkerReady.MessageIdentity.GetBytes()))
                            {
                                RegisterWorkers(multipart);
                            }
                            else
                            {
                                var handler = messageHandlers.Pop(new MessageIdentifier(multipart.GetMessageVersion(),
                                                                                        multipart.GetMessageIdentity(),
                                                                                        multipart.GetReceiverIdentity()));

                                multipart.SetSocketIdentity(handler.SocketId);
                                socket.SendMessage(new NetMQMessage(multipart.Frames));
                            }
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

        private void RegisterWorkers(MultipartMessage multipartMessage)
        {
            var message = new Message(multipartMessage);
            var payload = message.GetPayload<WorkerReady>();
            payload.MessageIdentities.ForEach(mi => messageHandlers.Push(new MessageIdentifier(mi.Version,
                                                                                               mi.Identity,
                                                                                               mi.ReceiverIdentity),
                                                                         new SocketIdentifier(multipartMessage.GetSocketIdentity())));
        }
    }
}