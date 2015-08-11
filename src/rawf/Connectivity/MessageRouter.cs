using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using rawf.Framework;
using rawf.Messaging;
using rawf.Messaging.Messages;
using rawf.Sockets;

namespace rawf.Connectivity
{
    public class MessageRouter : IMessageRouter
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task localRouting;
        private Task scaleOutRouting;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly ISocketFactory socketFactory;
        private readonly TaskCompletionSource<byte[]> localSocketIdentityPromise;
        private readonly IClusterConfigurationMonitor clusterConfigurationMonitor;
        private readonly IClusterConfiguration clusterConfiguration;
        private readonly IRouterConfiguration routerConfiguration;

        public MessageRouter(ISocketFactory socketFactory,
                             IInternalRoutingTable internalRoutingTable,
                             IExternalRoutingTable externalRoutingTable,
                             IClusterConfiguration clusterConfiguration,
                             IRouterConfiguration routerConfiguration,
                             IClusterConfigurationMonitor clusterConfigurationMonitor)
        {
            this.socketFactory = socketFactory;
            this.clusterConfiguration = clusterConfiguration;
            localSocketIdentityPromise = new TaskCompletionSource<byte[]>();
            this.internalRoutingTable = internalRoutingTable;
            this.externalRoutingTable = externalRoutingTable;
            this.clusterConfigurationMonitor = clusterConfigurationMonitor;
            this.routerConfiguration = routerConfiguration;
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
                using (var scaleOutFrontend = CreateScaleOutFrontendSocket())
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
                using (var localSocket = CreateRouterSocket())
                {
                    localSocketIdentityPromise.SetResult(localSocket.GetIdentity());
                    clusterConfigurationMonitor.RequestMessageHandlersRouting();

                    using (var scaleOutBackend = CreateScaleOutBackendSocket())
                    {
                        gateway.SignalAndWait(token);

                        while (!token.IsCancellationRequested)
                        {
                            try
                            {
                                var message = (Message) localSocket.ReceiveMessage(token);
                                if (message != null)
                                {
                                    var messageHandled = TryHandleServiceMessage(message, scaleOutBackend);

                                    if (!messageHandled)
                                    {
                                        var messageHandlerIdentifier = CreateMessageHandlerIdentifier(message);

                                        var handler = internalRoutingTable.Pop(messageHandlerIdentifier);
                                        if (handler != null)
                                        {
                                            message.SetSocketIdentity(handler.Identity);
                                            localSocket.SendMessage(message);
                                        }
                                        else
                                        {
                                            handler = externalRoutingTable.Pop(messageHandlerIdentifier);
                                            if (handler != null)
                                            {
                                                message.SetSocketIdentity(handler.Identity);
                                                scaleOutBackend.SendMessage(message);
                                            }
                                            else
                                            {
                                                Console.WriteLine(string.Format($"Handler not found! MSG: {messageHandlerIdentifier.Identity.GetString()}"));
                                            }
                                        }
                                    }
                                }
                            }
                            catch (NetMQException err)
                            {
                                Console.WriteLine(string.Format($"ERR: {err.ErrorCode} MSG: {err.Message} {err}"));
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

        private ISocket CreateScaleOutBackendSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            foreach (var peer in clusterConfiguration.GetClusterMembers())
            {
                socket.Connect(peer.Uri);
            }

            return socket;
        }

        private ISocket CreateScaleOutFrontendSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            socket.SetIdentity(routerConfiguration.ScaleOutAddress.Identity);
            socket.SetMandatoryRouting();
            socket.Connect(routerConfiguration.RouterAddress.Uri);
            socket.Bind(routerConfiguration.ScaleOutAddress.Uri);

            return socket;
        }

        private ISocket CreateRouterSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            socket.SetMandatoryRouting();
            socket.SetIdentity(routerConfiguration.RouterAddress.Identity);
            socket.Bind(routerConfiguration.RouterAddress.Uri);

            return socket;
        }

        private bool TryHandleServiceMessage(IMessage message, ISocket scaleOutBackend)
            => RegisterMessageHandler(message)
               || RegisterExternalRoute(message, scaleOutBackend)
               || RequestMessageHandlersRegistration(message)
               || UnregisterMessageHandlersRouting(message, scaleOutBackend);

        private bool UnregisterMessageHandlersRouting(IMessage message, ISocket scaleOutBackend)
        {
            var shouldHandle = IsUnregisterMessageHandlersRouting(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<UnregisterMessageHandlersRoutingMessage>();
                externalRoutingTable.RemoveRoute(new SocketIdentifier(payload.SocketIdentity));
                scaleOutBackend.Disconnect(new Uri(payload.Uri));

                Console.WriteLine($"Route removed URI:{payload.Uri} SOCKID:{payload.SocketIdentity.GetString()}");
            }

            return shouldHandle;
        }

        private bool RequestMessageHandlersRegistration(IMessage message)
        {
            var shouldHandle = IsMessageHandlersRoutingRequest(message);
            if (shouldHandle)
            {
                var messageIdentifiers = internalRoutingTable.GetMessageHandlerIdentifiers();
                if (messageIdentifiers.Any())
                {
                    clusterConfigurationMonitor.RegisterSelf(messageIdentifiers);
                }
            }

            return shouldHandle;
        }

        private bool RegisterExternalRoute(IMessage message ,ISocket scaleOutBackend)
        {
            var shouldHandle = IsExternalRouteRegistration(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<RegisterMessageHandlersRoutingMessage>();

                var handlerSocketIdentifier = new SocketIdentifier(payload.SocketIdentity);
                var uri = new Uri(payload.Uri);

                foreach (var registration in payload.MessageHandlers)
                {
                    try
                    {
                        var messageHandlerIdentifier = new MessageHandlerIdentifier(registration.Version, registration.Identity);
                        externalRoutingTable.Push(messageHandlerIdentifier, handlerSocketIdentifier, uri);
                        scaleOutBackend.Connect(uri);
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine(err);
                    }
                }
            }

            return shouldHandle;
        }

        private static bool IsMessageHandlersRoutingRequest(IMessage message)
            => Unsafe.Equals(RequestAllMessageHandlersRoutingMessage.MessageIdentity, message.Identity)
               || Unsafe.Equals(RequestNodeMessageHandlersRoutingMessage.MessageIdentity, message.Identity);

        private static bool IsExternalRouteRegistration(IMessage message)
            => Unsafe.Equals(RegisterMessageHandlersRoutingMessage.MessageIdentity, message.Identity);

        private static bool IsInternalHandlerRegistration(IMessage message)
            => Unsafe.Equals(RegisterMessageHandlersMessage.MessageIdentity, message.Identity);

        private bool IsUnregisterMessageHandlersRouting(IMessage message)
            => Unsafe.Equals(UnregisterMessageHandlersRoutingMessage.MessageIdentity, message.Identity);

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