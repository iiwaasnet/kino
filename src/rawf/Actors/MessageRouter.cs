using System;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly IMessageHandlerStack messageHandlers;
        private readonly IConnectivityProvider connectivityProvider;
        private readonly TaskCompletionSource<byte[]> localSocketIdentityPromise;

        public MessageRouter(IConnectivityProvider connectivityProvider, IMessageHandlerStack messageHandlers)
        {
            this.connectivityProvider = connectivityProvider;
            localSocketIdentityPromise = new TaskCompletionSource<byte[]>();
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
            var participantCount = 3;
            using (var gateway = new Barrier(participantCount))
            {
                localRouting = Task.Factory.StartNew(_ => RouteLocalMessages(cancellationTokenSource.Token, gateway),
                                                     TaskCreationOptions.LongRunning);
                scaleOutRouting = Task.Factory.StartNew(_ => RoutePeerMessages(cancellationTokenSource.Token, gateway),
                                                        TaskCreationOptions.LongRunning);

                gateway.SignalAndWait(cancellationTokenSource.Token);
            }
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel(true);
            localRouting.Wait();
            scaleOutRouting.Wait();
        }

        private void RoutePeerMessages(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var scaleOutFrontend = connectivityProvider.CreateFrontendScaleOutSocket())
                {
                    var localSocketIdentity = localSocketIdentityPromise.Task.Result;
                    gateway.SignalAndWait(token);

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var message = (Message) scaleOutFrontend.ReceiveMessage(token);
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

        private void RouteLocalMessages(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var localSocket = connectivityProvider.CreateRouterSocket())
                {
                    localSocketIdentityPromise.SetResult(localSocket.GetIdentity());

                    using (var scaleOutBackend = connectivityProvider.CreateBackendScaleOutSocket())
                    {
                        gateway.SignalAndWait(token);

                        while (!token.IsCancellationRequested)
                        {
                            try
                            {
                                var message = (Message) localSocket.ReceiveMessage(token);

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