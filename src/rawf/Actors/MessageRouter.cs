using System;
using System.Threading;
using NetMQ;
using rawf.Client;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Actors
{
    public class MessageRouter : IMessageRouter
    {
        private const string localEndpointAddress = "tcp://127.0.0.1:5555";
        private readonly CancellationTokenSource cancellationTokenSource;
        private Thread workingThread;
        private readonly MessageHandlerStack messageHandlers;
        private readonly NetMQContext context;
        private static readonly byte[] ReadyMessageIdentity = RegisterMessageHandlers.MessageIdentity.GetBytes();

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

                            if (IsReadyMessage(multipart))
                            {
                                RegisterWorkers(multipart);
                            }
                            else
                            {
                                var handler = messageHandlers.Pop(CreateMessageHandlerIdentifier(multipart));
                                if (handler != null)
                                {
                                    multipart.SetSocketIdentity(handler.SocketId);
                                    socket.SendMessage(new NetMQMessage(multipart.Frames));
                                }
                                else
                                {
                                    System.Console.WriteLine("No currently available handlers!");
                                }
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

        private static bool IsReadyMessage(MultipartMessage multipart)
        {
            return Unsafe.Equals(multipart.GetMessageIdentity(), ReadyMessageIdentity);
        }

        private void RegisterWorkers(MultipartMessage multipartMessage)
        {
            var message = new Message(multipartMessage);
            var payload = message.GetPayload<RegisterMessageHandlers>();
            var handlerSocketIdentifier = new SocketIdentifier(multipartMessage.GetSocketIdentity());

            foreach (var registration in payload.Registrations)
            {
                try
                {
                    messageHandlers.Push(CreateMessageHandlerIdentifier(registration), handlerSocketIdentifier);
                }
                catch (Exception err)
                {
                    System.Console.WriteLine(err);
                }
                
            }
        }

        private static MessageHandlerIdentifier CreateMessageHandlerIdentifier(MessageHandlerRegistration registration)
        {
            switch (registration.IdentityType)
            {
                case IdentityType.Actor:
                    return new ActorIdentifier(registration.Version, registration.Identity);
                case IdentityType.Callback:
                    return new CallbackIdentifier(registration.Version, registration.Identity);
                default:
                    throw new Exception($"IdentifierType {registration.IdentityType} is unknown!");
            }
        }

        private static MessageHandlerIdentifier CreateMessageHandlerIdentifier(MultipartMessage message)
        {
            var version = message.GetMessageVersion();
            var messageIdentity = message.GetMessageIdentity();
            var receiverIdentity = message.GetReceiverIdentity();

            if (Unsafe.Equals(receiverIdentity, MultipartMessage.EmptyFrame))
            {
                return new ActorIdentifier(version, messageIdentity);
            }

            return new CallbackIdentifier(version, receiverIdentity);
        }
    }
}