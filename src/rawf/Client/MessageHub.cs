using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using rawf.Actors;
using rawf.Connectivity;
using rawf.Framework;
using rawf.Messaging;
using rawf.Messaging.Messages;

namespace rawf.Client
{
    public class MessageHub : IMessageHub
    {
        private readonly NetMQContext context;
        private readonly CallbackHandlerStack callbackHandlers;
        private readonly string endpointAddress;
        private Task sending;
        private Task receiving;
        private readonly byte[] receivingSocketIdentity;
        private readonly BlockingCollection<CallbackRegistration> registrationsQueue;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly ManualResetEventSlim hubRegistered;

        public MessageHub(IConnectivityProvider connectivityProvider, IHostConfiguration config)
        {
            context = connectivityProvider.GetConnectivityContext();
            endpointAddress = config.GetRouterAddress();
            hubRegistered = new ManualResetEventSlim();
            callbackHandlers = new CallbackHandlerStack();
            registrationsQueue = new BlockingCollection<CallbackRegistration>(new ConcurrentQueue<CallbackRegistration>());
            cancellationTokenSource = new CancellationTokenSource();
            receivingSocketIdentity = Guid.NewGuid().ToString().GetBytes();
            receivingSocketIdentity = new byte[] {0, 1, 1, 1, 1, 10};
        }

        public void Start()
        {
            using (var gateway = new CountdownEvent(2))
            {
                receiving = Task.Factory.StartNew(_ => ReadReplies(cancellationTokenSource.Token, gateway),
                                                  TaskCreationOptions.LongRunning);
                sending = Task.Factory.StartNew(_ => SendClientRequests(cancellationTokenSource.Token, gateway),
                                                TaskCreationOptions.LongRunning);

                gateway.Wait(cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            sending.Wait();
            receiving.Wait();
        }

        private void SendClientRequests(CancellationToken token, CountdownEvent gateway)
        {
            try
            {
                using (var socket = CreateSendingSocket(context))
                {
                    gateway.Signal();

                    foreach (var callbackRegistration in registrationsQueue.GetConsumingEnumerable(token))
                    {
                        try
                        {
                            var message = (Message) callbackRegistration.Message;
                            var promise = callbackRegistration.Promise;
                            var callbackPoint = callbackRegistration.CallbackPoint;

                            message.RegisterCallbackPoint(callbackPoint.MessageIdentity, receivingSocketIdentity);

                            callbackHandlers.Push(new CallbackHandlerKey
                                                  {
                                                      Version = message.Version,
                                                      Identity = callbackPoint.MessageIdentity,
                                                      Correlation = message.CorrelationId
                                                  },
                                                  promise);
                            callbackHandlers.Push(new CallbackHandlerKey
                                                  {
                                                      Version = message.Version,
                                                      Identity = ExceptionMessage.MessageIdentity,
                                                      Correlation = message.CorrelationId
                                                  },
                                                  promise);


                            var messageOut = new MultipartMessage(message);
                            socket.SendMessage(new NetMQMessage(messageOut.Frames));
                        }
                        catch (Exception err)
                        {
                            Console.WriteLine(err);
                        }
                    }
                    registrationsQueue.Dispose();
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }


        private void ReadReplies(CancellationToken token, CountdownEvent gateway)
        {
            try
            {
                using (var socket = CreateReceivingSocket(context, receivingSocketIdentity))
                {
                    gateway.Signal();

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var request = socket.ReceiveMessage();
                            var multipart = new MultipartMessage(request);
                            var messageIn = new Message(multipart);
                            var callback = (Promise) callbackHandlers.Pop(new CallbackHandlerKey
                                                                          {
                                                                              Version = multipart.GetMessageVersion(),
                                                                              Identity = multipart.GetMessageIdentity(),
                                                                              Correlation = multipart.GetCorrelationId()
                                                                          });
                            callback?.SetResult(messageIn);
                        }
                        catch (Exception err)
                        {
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

        private NetMQSocket CreateReceivingSocket(NetMQContext context, byte[] socketIdentity = null)
        {
            var socket = context.CreateDealerSocket();
            socket.Options.Identity = socketIdentity;
            socket.Connect(endpointAddress);

            return socket;
        }

        private NetMQSocket CreateSendingSocket(NetMQContext context)
        {
            var socket = context.CreateDealerSocket();
            socket.Connect(endpointAddress);

            RegisterMessageHub(socket);

            return socket;
        }

        private void RegisterMessageHub(DealerSocket socket)
        {
            var rdyMessage = Message.Create(new RegisterMessageHandlers
                                            {
                                                SocketIdentity = receivingSocketIdentity,
                                                Registrations = new[]
                                                                {
                                                                    new MessageHandlerRegistration
                                                                    {
                                                                        Version = Message.CurrentVersion,
                                                                        Identity = receivingSocketIdentity,
                                                                        IdentityType = IdentityType.Callback
                                                                    }
                                                                }
                                            }, RegisterMessageHandlers.MessageIdentity);
            var messageOut = new MultipartMessage(rdyMessage);
            socket.SendMessage(new NetMQMessage(messageOut.Frames));

            hubRegistered.Set();
        }

        public IPromise EnqueueRequest(IMessage message, ICallbackPoint callbackPoint)
        {
            hubRegistered.Wait();

            var promise = new Promise();

            registrationsQueue.Add(new CallbackRegistration
                                   {
                                       Message = message,
                                       Promise = promise,
                                       CallbackPoint = callbackPoint
                                   });

            return promise;
        }
    }
}