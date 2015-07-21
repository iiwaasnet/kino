using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using rawf.Connectivity;
using rawf.Messaging;
using rawf.Messaging.Messages;
using rawf.Sockets;

namespace rawf.Client
{
    public class MessageHub : IMessageHub
    {
        private readonly NetMQContext context;
        private readonly ICallbackHandlerStack callbackHandlers;
        private readonly IConnectivityProvider connectivityProvider;
        private Task sending;
        private Task receiving;
        private readonly TaskCompletionSource<byte[]> receivingSocketIdentityPromise;
        private readonly BlockingCollection<CallbackRegistration> registrationsQueue;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly ManualResetEventSlim hubRegistered;

        public MessageHub(IConnectivityProvider connectivityProvider, ICallbackHandlerStack callbackHandlers)
        {
            this.connectivityProvider = connectivityProvider;
            receivingSocketIdentityPromise = new TaskCompletionSource<byte[]>();
            hubRegistered = new ManualResetEventSlim();
            this.callbackHandlers = callbackHandlers;
            registrationsQueue = new BlockingCollection<CallbackRegistration>(new ConcurrentQueue<CallbackRegistration>());
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            var participantCount = 3;
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
                using (var socket = connectivityProvider.CreateClientSendingSocket())
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


        private void ReadReplies(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var socket = connectivityProvider.CreateClientReceivingSocket())
                {
                    receivingSocketIdentityPromise.SetResult(socket.GetIdentity());
                    gateway.SignalAndWait(token);

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var messageIn = socket.ReceiveMessage(token);
                            var callback = (Promise) callbackHandlers.Pop(new CallbackHandlerKey
                                                                          {
                                                                              Version = messageIn.Version,
                                                                              Identity = messageIn.Identity,
                                                                              Correlation = messageIn.CorrelationId
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

        private void RegisterMessageHub(ISocket socket, byte[] receivingSocketIdentity)
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