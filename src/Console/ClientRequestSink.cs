using System;
using System.Collections.Concurrent;
using System.Threading;
using Console.Messages;
using NetMQ;

namespace Console
{
    public class ClientRequestSink : IClientRequestSink
    {
        private readonly NetMQContext context;
        private readonly CallbackHandlerStack callbackHandlers;
        private const string endpointAddress = Program.EndpointAddress;
        private Thread sendingThread;
        private Thread receivingThread;
        private readonly byte[] receivingSocketIdentity;
        private readonly BlockingCollection<CallbackRegistration> registrationsQueue;
        private readonly CancellationTokenSource cancellationTokenSource;

        public ClientRequestSink(NetMQContext context)
        {
            this.context = context;
            callbackHandlers = new CallbackHandlerStack();
            registrationsQueue = new BlockingCollection<CallbackRegistration>(new ConcurrentQueue<CallbackRegistration>());
            cancellationTokenSource = new CancellationTokenSource();
            receivingSocketIdentity = Guid.NewGuid().ToByteArray();
        }

        public void Start()
        {
            receivingThread = new Thread(_ => ReadReplies(cancellationTokenSource.Token));
            sendingThread = new Thread(_ => SendClientRequests(cancellationTokenSource.Token));
            sendingThread.Start();
            receivingThread.Start();
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            sendingThread.Join();
            receivingThread.Join();
        }

        private void SendClientRequests(CancellationToken token)
        {
            try
            {
                using (var socket = CreateSocket(context))
                {
                    foreach (var callbackRegistration in registrationsQueue.GetConsumingEnumerable(token))
                    {
                        try
                        {
                            var message = callbackRegistration.Message;
                            var promise = callbackRegistration.Promise;

                            callbackHandlers.Push(new MessageIdentifier(message.Version.GetBytes(),
                                                                        message.CallbackIdentity,
                                                                        message.CallbackReceiverIdentity),
                                                  promise);

                            var rdyMessage = Message.Create(new WorkerReady {MessageIdentities = new []
                                                                                                 {
                                                                                                     new MessageIdentity
                                                                                                     {
                                                                                                         Identity = message.CallbackIdentity.GetString(),
                                                                                                         Version = message.Version,
                                                                                                         ReceiverIdentity = message.CallbackReceiverIdentity.GetString()
                                                                                                     }
                                                                                                 }}, WorkerReady.MessageIdentity);
                            var messageOut = new MultipartMessage(rdyMessage);
                            socket.SendMessage(new NetMQMessage(messageOut.Frames));

                            Thread.Sleep(TimeSpan.FromSeconds(5));

                            messageOut = new MultipartMessage(message, receivingSocketIdentity);
                            socket.SendMessage(new NetMQMessage(messageOut.Frames));
                        }
                        catch (Exception err)
                        {
                            System.Console.WriteLine(err);
                        }
                    }
                    registrationsQueue.Dispose();
                }
            }
            catch (Exception err)
            {
                System.Console.WriteLine(err);
            }
        }


        private void ReadReplies(CancellationToken token)
        {
            try
            {
                using (var socket = CreateSocket(context, receivingSocketIdentity))
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var request = socket.ReceiveMessage();
                            var multipart = new MultipartMessage(request);
                            var messageIn = new Message(multipart);
                            var callback = (Promise) callbackHandlers.Pop(new MessageIdentifier(multipart.GetMessageVersion(),
                                                                                                multipart.GetMessageIdentity(),
                                                                                                multipart.GetReceiverIdentity()));
                            callback?.SetResult(messageIn);
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

        private NetMQSocket CreateSocket(NetMQContext context, byte[] socketIdentity = null)
        {
            var socket = context.CreateDealerSocket();
            //socket.Options.RouterMandatory = true;
            if (socketIdentity != null)
            {
                socket.Options.Identity = socketIdentity;
            }
            socket.Connect(endpointAddress);

            return socket;
        }

        public IPromise EnqueueRequest(IMessage message)
        {
            var promise = new Promise();

            registrationsQueue.Add(new CallbackRegistration
                                   {
                                       Message = message,
                                       Promise = promise
                                   });

            return promise;
        }
    }

    internal class CallbackRegistration
    {
        internal IPromise Promise { get; set; }
        internal IMessage Message { get; set; }
    }
}