using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using rawf.Connectivity;
using rawf.Framework;
using rawf.Messaging;
using rawf.Messaging.Messages;

namespace rawf.Backend
{
    public class MessageRouter : IMessageRouter
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task localRouting;
        private Task scaleOutRouting;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly IConnectivityProvider connectivityProvider;
        private readonly TaskCompletionSource<byte[]> localSocketIdentityPromise;
        private readonly IClusterConfigurationMonitor clusterConfigurationMonitor;
        private readonly INodeConfiguration nodeConfiguration;

        public MessageRouter(IConnectivityProvider connectivityProvider,
                             IInternalRoutingTable internalRoutingTable,
                             IExternalRoutingTable externalRoutingTable,
                             INodeConfiguration nodeConfiguration = null,
                             IClusterConfigurationMonitor clusterConfigurationMonitor = null)
        {
            this.connectivityProvider = connectivityProvider;
            localSocketIdentityPromise = new TaskCompletionSource<byte[]>();
            this.internalRoutingTable = internalRoutingTable;
            this.externalRoutingTable = externalRoutingTable;
            this.clusterConfigurationMonitor = clusterConfigurationMonitor;
            this.nodeConfiguration = nodeConfiguration;
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            const int participantCount = 3;
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
                            if (message != null)
                            {
                                message.SetSocketIdentity(localSocketIdentity);
                                scaleOutFrontend.SendMessage(message);
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

        private void RouteLocalMessages(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var localSocket = connectivityProvider.CreateRouterSocket())
                {
                    localSocketIdentityPromise.SetResult(localSocket.GetIdentity());
                    clusterConfigurationMonitor.RequestMessageHandlersRouting();

                    using (var scaleOutBackend = connectivityProvider.CreateScaleOutBackendSocket())
                    {
                        gateway.SignalAndWait(token);

                        while (!token.IsCancellationRequested)
                        {
                            try
                            {
                                var message = (Message) localSocket.ReceiveMessage(token);
                                if (message != null)
                                {
                                    var messageHandled = TryHandleServiceMessage(message);

                                    if (!messageHandled)
                                    {
                                        var messageHandlerIdentifier = CreateMessageHandlerIdentifier(message);

                                        var handler = internalRoutingTable.Pop(messageHandlerIdentifier);
                                        if (handler != null)
                                        {
                                            message.SetSocketIdentity(handler.SocketId);
                                            localSocket.SendMessage(message);
                                        }
                                        else
                                        {
                                            handler = externalRoutingTable.Pop(messageHandlerIdentifier);
                                            if (handler != null)
                                            {
                                                message.SetSocketIdentity(handler.SocketId);
                                                scaleOutBackend.SendMessage(message);
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
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }

        private bool TryHandleServiceMessage(IMessage message)
        {
            return RegisterMessageHandler(message)
                   || RegisterExternalRoute(message)
                   || SendMessageHandlersRegistration(message);
        }

        private bool SendMessageHandlersRegistration(IMessage message)
        {
            var shouldHandle = IsMessageHandlersRoutingRequest(message);

            if (shouldHandle)
            {
                var payload = message.GetPayload<RequestMessageHandlersRoutingMessage>();

                if (NotSelfSentMessage(payload.RequestorSocketIdentity))
                {
                    clusterConfigurationMonitor.RegisterSelf(internalRoutingTable.GetMessageHandlerIdentifiers());
                }
            }

            return shouldHandle;
        }

        private bool RegisterExternalRoute(IMessage message)
        {
            var shouldHandle = IsExternalRouteRegistration(message);

            if (shouldHandle)
            {
                var payload = message.GetPayload<RegisterMessageHandlersRoutingMessage>();

                if (NotSelfSentMessage(payload.SocketIdentity))
                {
                    var handlerSocketIdentifier = new SocketIdentifier(payload.SocketIdentity);
                    var uri = new Uri(payload.Uri);

                    foreach (var registration in payload.MessageHandlers)
                    {
                        try
                        {
                            var messageHandlerIdentifier = new MessageHandlerIdentifier(registration.Version, registration.Identity);
                            externalRoutingTable.Push(messageHandlerIdentifier, handlerSocketIdentifier, uri);
                        }
                        catch (Exception err)
                        {
                            Console.WriteLine(err);
                        }
                    }
                }
            }

            return shouldHandle;
        }

        private bool NotSelfSentMessage(byte[] socketIdentity)
            => !Unsafe.Equals(nodeConfiguration.ScaleOutAddress.Identity, socketIdentity);

        private static bool IsMessageHandlersRoutingRequest(IMessage message)
            => Unsafe.Equals(RequestMessageHandlersRoutingMessage.MessageIdentity, message.Identity);

        private static bool IsExternalRouteRegistration(IMessage message)
            => Unsafe.Equals(RegisterMessageHandlersRoutingMessage.MessageIdentity, message.Identity);

        private static bool IsInternalHandlerRegistration(IMessage message)
            => Unsafe.Equals(RegisterMessageHandlersMessage.MessageIdentity, message.Identity);

        private bool RegisterMessageHandler(IMessage message)
        {
            var shouldHandle = IsInternalHandlerRegistration(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<RegisterMessageHandlersMessage>();
                var handlerSocketIdentifier = new SocketIdentifier(payload.SocketIdentity);

                var handlers = UpdateLocalRoutingTable(payload, handlerSocketIdentifier);

                clusterConfigurationMonitor.RegisterSelf(handlers);
            }

            return shouldHandle;
        }

        private IEnumerable<MessageHandlerIdentifier> UpdateLocalRoutingTable(RegisterMessageHandlersMessage payload,
                                                                              SocketIdentifier handlerSocketIdentifier)
        {
            var handlers = new List<MessageHandlerIdentifier>();

            foreach (var registration in payload.MessageHandlers)
            {
                try
                {
                    var messageHandlerIdentifier = new MessageHandlerIdentifier(registration.Version, registration.Identity);
                    internalRoutingTable.Push(messageHandlerIdentifier, handlerSocketIdentifier);
                    handlers.Add(messageHandlerIdentifier);
                }
                catch (Exception err)
                {
                    Console.WriteLine(err);
                }
            }

            return handlers;
        }

        private static MessageHandlerIdentifier CreateMessageHandlerIdentifier(IMessage message)
            => new MessageHandlerIdentifier(message.Version,
                                            message.ReceiverIdentity.IsSet()
                                                ? message.ReceiverIdentity
                                                : message.Identity);
    }
}