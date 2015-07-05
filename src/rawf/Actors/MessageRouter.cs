using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using rawf.Client;
using rawf.Connectivity;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Actors
{
    public class MessageRouter : IMessageRouter
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task localRouting;
        private Task scaleOutRouting;
        private readonly MessageHandlerStack messageHandlers;
        private readonly NetMQContext context;
        private static readonly byte[] ReadyMessageIdentity = RegisterMessageHandlers.MessageIdentity.GetBytes();
        private readonly byte[] scaleOutSocketIdentity = {1, 1, 1, 1, 1};
        private readonly byte[] localSocketIdentity = {2, 2, 2, 2, 2};
        private readonly IConnectivityProvider connectivityProvider;

        public MessageRouter(IConnectivityProvider connectivityProvider)
        {
            this.connectivityProvider = connectivityProvider;
            context = (NetMQContext) connectivityProvider.GetConnectivityContext();
            messageHandlers = new MessageHandlerStack();
            cancellationTokenSource = new CancellationTokenSource();
        }

        private NetMQSocket CreateSocket(NetMQContext context)
        {
            var socket = context.CreateRouterSocket();
            socket.Options.RouterMandatory = true;
            socket.Options.Identity = localSocketIdentity;
            socket.Bind(connectivityProvider.GetLocalEndpointAddress());

            return socket;
        }

        public void Start()
        {
            localRouting = Task.Factory.StartNew(_ => RouteLocalMessages(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);
            scaleOutRouting = Task.Factory.StartNew(_ => RoutePeerMessages(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);
        }        

        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            localRouting.Wait();
            scaleOutRouting.Wait();
        }

        private void RoutePeerMessages(CancellationToken token)
        {
            try
            {
                using (var peeringFrontend = CreatePeeringFrontendSocket(context))
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var message = peeringFrontend.ReceiveMessage();
                            var multipart = new MultipartMessage(message, true);
                            multipart.SetSocketIdentity(localSocketIdentity);
                            peeringFrontend.SendMessage(new NetMQMessage(multipart.Frames));
                            
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

        private void RouteLocalMessages(CancellationToken token)
        {
            try
            {
                using (var localSocket = CreateSocket(context))
                {
                    using (var peeringBackend = CreatePeeringBackendSocket(context))
                    {
                        while (!token.IsCancellationRequested)
                        {
                            try
                            {
                                var request = localSocket.ReceiveMessage();
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
                                        localSocket.SendMessage(new NetMQMessage(multipart.Frames));
                                    }
                                    else
                                    {
                                        Console.WriteLine("No currently available handlers!");
                                        multipart.SetSocketIdentity(scaleOutSocketIdentity);
                                        peeringBackend.SendMessage(new NetMQMessage(multipart.Frames));
                                    }
                                }
                            }
                            catch (Exception err)
                            {
                                Console.WriteLine(err);
                            }
                        }
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }

        private NetMQSocket CreatePeeringBackendSocket(NetMQContext context)
        {
            var socket = context.CreateRouterSocket();
            socket.Options.Identity = scaleOutSocketIdentity;
            socket.Options.RouterMandatory = true;
            foreach (var peer in connectivityProvider.GetScaleOutCluster())
            {
                socket.Connect(peer);
            }

            return socket;
        }

        private NetMQSocket CreatePeeringFrontendSocket(NetMQContext context)
        {
            var socket = context.CreateRouterSocket();
            socket.Options.Identity = scaleOutSocketIdentity;
            socket.Options.RouterMandatory = true;
            socket.Connect(connectivityProvider.GetLocalEndpointAddress());
            socket.Bind(connectivityProvider.GetLocalScaleOutAddress());

            return socket;
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
                    Console.WriteLine(err);
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