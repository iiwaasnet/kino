using System;
using System.Threading;
using System.Threading.Tasks;
using rawf.Connectivity;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Backend
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
                using (var scaleOutFrontend = connectivityProvider.CreateScaleOutFrontendSocket())
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

                    using (var scaleOutBackend = connectivityProvider.CreateScaleOutBackendSocket())
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
                                        // NOTE: scaleOutBackend socket identities should be received with configuration,
                                        // so that the next socket to which this message was not yet routed is selected
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
                case IdentityType.Callback:
                    return new MessageHandlerIdentifier(registration.Version, registration.Identity);
                default:
                    throw new Exception($"IdentifierType {registration.IdentityType} is unknown!");
            }
        }

        private static MessageHandlerIdentifier CreateMessageHandlerIdentifier(IMessage message)
        {
            return new MessageHandlerIdentifier(message.Version,
                                                message.ReceiverIdentity.IsSet()
                                                    ? message.ReceiverIdentity
                                                    : message.Identity);
        }
    }
}