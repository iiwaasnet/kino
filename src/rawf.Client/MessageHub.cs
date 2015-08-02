using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using rawf.Connectivity;
using rawf.Messaging;
using rawf.Messaging.Messages;
using rawf.Sockets;

namespace rawf.Client
{
    public class MessageHub : IMessageHub
    {
        private readonly ICallbackHandlerStack callbackHandlers;
        private readonly ISocketFactory socketFactory;
        private Task sending;
        private Task receiving;
        private readonly TaskCompletionSource<byte[]> receivingSocketIdentityPromise;
        private readonly BlockingCollection<CallbackRegistration> registrationsQueue;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly ManualResetEventSlim hubRegistered;
        private readonly IMessageHubConfiguration config;

        public MessageHub(ISocketFactory socketFactory,
                          ICallbackHandlerStack callbackHandlers,
                          IMessageHubConfiguration config)
        {
            this.socketFactory = socketFactory;
            this.config = config;
            receivingSocketIdentityPromise = new TaskCompletionSource<byte[]>();
            hubRegistered = new ManualResetEventSlim();
            this.callbackHandlers = callbackHandlers;
            registrationsQueue = new BlockingCollection<CallbackRegistration>(new ConcurrentQueue<CallbackRegistration>());
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            const int participantCount = 3;
            using (var gateway = new Barrier(participantCount))
            {
                receiving = Task.Factory.StartNew(_ => ReadReplies(cancellationTokenSource.Token, gateway),
                                                  TaskCreationOptions.LongRunning);
                sending = Task.Factory.StartNew(_ => SendClientRequests(cancellationTokenSource.Token, gateway),
                                                TaskCreationOptions.LongRunning);

                gateway.SignalAndWait(cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            sending.Wait();
            receiving.Wait();
        }

        private void SendClientRequests(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var socket = CreateOneWaySocket())
                {
                    var receivingSocketIdentity = receivingSocketIdentityPromise.Task.Result;
                    RegisterMessageHub(socket, receivingSocketIdentity);
                    gateway.SignalAndWait(token);

                    foreach (var callbackRegistration in registrationsQueue.GetConsumingEnumerable(token))
                    {
                        try
                        {
                            var message = (Message) callbackRegistration.Message;
                            var promise = callbackRegistration.Promise;
                            var callbackPoint = callbackRegistration.CallbackPoint;

                            message.RegisterCallbackPoint(callbackPoint.MessageIdentity, receivingSocketIdentity);

                            callbackHandlers.Push(new CorrelationId(message.CorrelationId),
                                                  promise,
                                                  new[]
                                                  {
                                                      new MessageHandlerIdentifier(message.Version, callbackPoint.MessageIdentity),
                                                      new MessageHandlerIdentifier(message.Version, ExceptionMessage.MessageIdentity)
                                                  });

                            socket.SendMessage(message);
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

        private ISocket CreateOneWaySocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(config.RouterUri);

            return socket;
        }

        private void ReadReplies(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var socket = CreateRoutableSocket())
                {
                    receivingSocketIdentityPromise.SetResult(socket.GetIdentity());
                    gateway.SignalAndWait(token);

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var message = socket.ReceiveMessage(token);
                            if (message != null)
                            {
                                var callback = (Promise) callbackHandlers.Pop(new CallbackHandlerKey
                                                                              {
                                                                                  Version = message.Version,
                                                                                  Identity = message.Identity,
                                                                                  Correlation = message.CorrelationId
                                                                              });
                                callback?.SetResult(message);
                            }
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

        private ISocket CreateRoutableSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.SetIdentity(SocketIdentifier.CreateNew());
            socket.Connect(config.RouterUri);

            return socket;
        }

        private void RegisterMessageHub(ISocket socket, byte[] receivingSocketIdentity)
        {
            var rdyMessage = Message.Create(new RegisterMessageHandlersMessage
                                            {
                                                SocketIdentity = receivingSocketIdentity,
                                                MessageHandlers = new[]
                                                                  {
                                                                      new MessageHandlerRegistration
                                                                      {
                                                                          Version = Message.CurrentVersion,
                                                                          Identity = receivingSocketIdentity
                                                                      }
                                                                  }
                                            },
                                            RegisterMessageHandlersMessage.MessageIdentity);
            socket.SendMessage(rdyMessage);

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