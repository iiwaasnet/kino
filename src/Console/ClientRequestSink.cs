using System;
using System.Collections.Generic;
using System.Threading;
using Console.Messages;
using NetMQ;

namespace Console
{
    public class ClientRequestSink : IClientRequestSink
    {
        private readonly NetMQContext context;
        private IDictionary<string, MessageHandler> messageHandlers;
        private const string endpointAddress = "inproc://local";
        private Thread workingThread;
        private readonly CancellationTokenSource cancellationTokenSource;

        public ClientRequestSink(NetMQContext context)
        {
            this.context = context;
        }

        public void Start()
        {
            workingThread = new Thread(_ => StartProcessingRequests(cancellationTokenSource.Token));
        }


        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            workingThread.Join();
        }

        private void StartProcessingRequests(CancellationToken token)
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

                            var messageOut = handler(messageIn);

                            if (messageOut != null)
                            {
                                var response = new MultipartMessage(messageOut, socket.Options.Identity);
                                socket.SendMessage(new NetMQMessage(response.Frames));
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

        private NetMQSocket CreateSocket(NetMQContext context)
        {
            var socket = context.CreateDealerSocket();
            socket.Options.RouterMandatory = true;
            socket.Options.Identity = Guid.NewGuid().ToByteArray();
            socket.Connect(endpointAddress);

            return socket;
        }

        public IPromise<T> EnqueueRequest<T>(IMessage message) where T : IMessage
        {
            throw new NotImplementedException();
        }
    }
}