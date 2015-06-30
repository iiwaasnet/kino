﻿using System;
using System.Collections.Concurrent;
using System.Threading;
using Console.Messages;
using NetMQ;
using NetMQ.Sockets;

namespace Console
{
    public class MessageHub : IMessageHub
    {
        private readonly NetMQContext context;
        private readonly CallbackHandlerStack callbackHandlers;
        private const string endpointAddress = Program.EndpointAddress;
        private Thread sendingThread;
        private Thread receivingThread;
        private readonly byte[] receivingSocketIdentity;
        private readonly BlockingCollection<CallbackRegistration> registrationsQueue;
        private readonly CancellationTokenSource cancellationTokenSource;

        public MessageHub(NetMQContext context)
        {
            this.context = context;
            callbackHandlers = new CallbackHandlerStack();
            registrationsQueue = new BlockingCollection<CallbackRegistration>(new ConcurrentQueue<CallbackRegistration>());
            cancellationTokenSource = new CancellationTokenSource();
            receivingSocketIdentity = Guid.NewGuid().ToString().GetBytes();
            receivingSocketIdentity = new byte[] {0, 1, 1, 1, 1, 10};
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
                using (var socket = CreateSendingSocket(context))
                {
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


                            var messageOut = new MultipartMessage(message);
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
                using (var socket = CreateReceivingSocket(context, receivingSocketIdentity))
                {
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

            RegisterRequestSink(socket);

            return socket;
        }

        private void RegisterRequestSink(DealerSocket socket)
        {
            var rdyMessage = Message.Create(new RegisterMessageHandlers
                                            {
                                                Registrations = new[]
                                                                {
                                                                    new MessageHandlerRegistration
                                                                    {
                                                                        Version = Message.CurrentVersion.GetBytes(),
                                                                        Identity = receivingSocketIdentity,
                                                                        IdentityType = IdentityType.Callback
                                                                    }
                                                                }
                                            }, RegisterMessageHandlers.MessageIdentity);
            var messageOut = new MultipartMessage(rdyMessage, receivingSocketIdentity);
            socket.SendMessage(new NetMQMessage(messageOut.Frames));
        }

        public IPromise EnqueueRequest(IMessage message, ICallbackPoint callbackPoint)
        {
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