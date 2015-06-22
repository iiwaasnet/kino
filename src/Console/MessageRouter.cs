using System;
using System.Threading;
using Console.Messages;
using Framework;
using NetMQ;

namespace Console
{
    public class MessageRouter : IMessageRouter
    {
        private const string localEndpointAddress = "inproc://local";
        private readonly CancellationTokenSource cancellationTokenSource;
        private Thread workingThread;
        private readonly MessageHandlerStack messageHandlers;
        private readonly NetMQContext context;

        public MessageRouter(NetMQContext context)
        {
            this.context = context;
            cancellationTokenSource = new CancellationTokenSource();
        }

        private NetMQSocket CreateSocket(NetMQContext context)
        {
            var socket = context.CreateRouterSocket();
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
                            var multipart = new MultipartMessage(request);


                            if (multipart.GetMessageIdentity() == WorkerReady.MessageIdentity.GetBytes())
                            {
                                RegisterWorkers(multipart);
                            }
                            else
                            {
                                var handler = messageHandlers.Pop(new MessageIdentifier(multipart.GetMessageVersion(),
                                                                                        multipart.GetMessageIdentity()));

                                multipart.SetSocketIdentity(handler.SocketId);
                                socket.SendMessage(new NetMQMessage(multipart.Frames));
                            }
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

        private void RegisterWorkers(MultipartMessage multipartMessage)
        {
            var message = new Message(multipartMessage);
            var payload = message.GetPayload<WorkerReady>();
            payload
                .MessageIdentities
                .ForEach(mi => messageHandlers.Push(new MessageIdentifier(multipartMessage.GetMessageVersion(),
                                                                          multipartMessage.GetMessageIdentity()),
                                                    new SocketIdentifier(multipartMessage.GetSocketIdentity())));
        }
    }
}