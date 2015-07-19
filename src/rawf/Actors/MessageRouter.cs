using System;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using rawf.Client;
using rawf.Connectivity;
using rawf.Framework;
using rawf.Messaging;
using rawf.Sockets;

namespace rawf.Actors
{
    public class MessageRouter : IMessageRouter
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task localRouting;
        private Task scaleOutRouting;
        private readonly IMessageHandlerStack messageHandlers;
        private byte[] localSocketIdentity;
        private readonly IConnectivityProvider connectivityProvider;

        public MessageRouter(IConnectivityProvider connectivityProvider, IMessageHandlerStack messageHandlers)
        {
            this.connectivityProvider = connectivityProvider;
            this.messageHandlers = messageHandlers;
            cancellationTokenSource = new CancellationTokenSource();
        }

        //private ISocket CreateSocket()
        //{
        //    //var socket = connectivityProvider.CreateRouterSocket();
        //    //socket.SetMandatoryRouting();
        //    //socket.SetIdentity(localSocketIdentity);
        //    //socket.Bind(config.GetRouterAddress());

        //    //return socket;
        //}

        public void Start()
        {
            using (var gateway = new CountdownEvent(2))
            {
                localRouting = Task.Factory.StartNew(_ => RouteLocalMessages(cancellationTokenSource.Token, gateway),
                                                     TaskCreationOptions.LongRunning);
                scaleOutRouting = Task.Factory.StartNew(_ => RoutePeerMessages(cancellationTokenSource.Token, gateway),
                                                        TaskCreationOptions.LongRunning);

                gateway.Wait(cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            localRouting.Wait();
            scaleOutRouting.Wait();
        }

        private void RoutePeerMessages(CancellationToken token, CountdownEvent gateway)
        {
            try
            {
                using (var scaleOutFrontend = connectivityProvider.CreateFrontendScaleOutSocket())
                {
                    gateway.Signal();

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var message = (Message)scaleOutFrontend.ReceiveMessage(token);
                            message.SetSocketIdentity(localSocketIdentity);
                            scaleOutFrontend.SendMessage(message);
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

        private void RouteLocalMessages(CancellationToken token, CountdownEvent gateway)
        {
            try
            {
                using (var localSocket = connectivityProvider.CreateRouterSocket())
                {
                    localSocketIdentity = localSocket.GetIdentity();

                    using (var scaleOutBackend = connectivityProvider.CreateBackendScaleOutSocket())
                    {
                        gateway.Signal();

                        while (!token.IsCancellationRequested)
                        {
                            try
                            {
                                var message = (Message)localSocket.ReceiveMessage(token);

                                if (IsReadyMessage(message))
                                {
                                    RegisterWorkers(message);
                                }
                                else
                                {
                                    var handler = messageHandlers.Pop(CreateMessageHandlerIdentifier(message));
                                    if (handler != null)
                                    {
                                        message.SetSocketIdentity(handler.SocketId);
                                        localSocket.SendMessage(message);
                                    }
                                    else
                                    {
                                        //Console.WriteLine("No currently available handlers!");

                                        message.SetSocketIdentity(scaleOutBackend.GetIdentity());
                                        scaleOutBackend.SendMessage(message);
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

        //private ISocket CreateScaleOutBackendSocket()
        //{
        //    //var socket = connectivityProvider.CreateRouterSocket();
        //    //socket.SetIdentity(scaleOutSocketIdentity);
        //    //socket.SetMandatoryRouting();
        //    //foreach (var peer in config.GetScaleOutCluster())
        //    //{
        //    //    socket.Connect(peer);
        //    //}

        //    //return socket;
        //}

        //private ISocket CreateScaleOutFrontendSocket()
        //{
        //    //var socket = connectivityProvider.CreateRouterSocket();
        //    //socket.SetIdentity(scaleOutSocketIdentity);
        //    //socket.SetMandatoryRouting();
        //    //socket.Connect(config.GetRouterAddress());
        //    //socket.Bind(config.GetLocalScaleOutAddress());

        //    //return socket;
        //}

        private static bool IsReadyMessage(IMessage message)
        {
            return Unsafe.Equals(message.Identity, RegisterMessageHandlers.MessageIdentity);
        }

        private void RegisterWorkers(IMessage message)
        {
            var payload = message.GetPayload<RegisterMessageHandlers>();
            var handlerSocketIdentifier = new SocketIdentifier(payload.SocketIdentity);

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

        private static MessageHandlerIdentifier CreateMessageHandlerIdentifier(IMessage message)
        {
            var version = message.Version;
            var messageIdentity = message.Identity;
            var receiverIdentity = message.ReceiverIdentity;

            if (receiverIdentity.IsSet())
            {
                return new CallbackIdentifier(version, receiverIdentity);
            }

            return new ActorIdentifier(version, messageIdentity);
        }
    }
}