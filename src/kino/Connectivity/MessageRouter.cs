using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Diagnostics;
using kino.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Sockets;
using NetMQ;

namespace kino.Connectivity
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
        private readonly IClusterMonitor clusterMonitor;
        private readonly RouterConfiguration routerConfiguration;
        private readonly ILogger logger;
        private readonly IMessageTracer messageTracer;
        private static readonly TimeSpan TerminationWaitTimeout = TimeSpan.FromSeconds(3);

        public MessageRouter(ISocketFactory socketFactory,
                             IInternalRoutingTable internalRoutingTable,
                             IExternalRoutingTable externalRoutingTable,
                             RouterConfiguration routerConfiguration,
                             IClusterMonitor clusterMonitor,
                             IMessageTracer messageTracer,
                             ILogger logger)
        {
            this.logger = logger;
            this.messageTracer = messageTracer;
            this.socketFactory = socketFactory;
            localSocketIdentityPromise = new TaskCompletionSource<byte[]>();
            this.internalRoutingTable = internalRoutingTable;
            this.externalRoutingTable = externalRoutingTable;
            this.clusterMonitor = clusterMonitor;
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
            cancellationTokenSource.Cancel();
            localRouting.Wait(TerminationWaitTimeout);
            scaleOutRouting.Wait(TerminationWaitTimeout);
            cancellationTokenSource.Dispose();
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

                                messageTracer.ReceivedFromOtherNode(message);
                            }
                        }
                        catch (Exception err)
                        {
                            logger.Error(err);
                        }
                    }
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private void RouteLocalMessages(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var localSocket = CreateRouterSocket())
                {
                    localSocketIdentityPromise.SetResult(localSocket.GetIdentity());
                    clusterMonitor.RequestClusterRoutes();

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
                                    var _ = TryHandleServiceMessage(message, scaleOutBackend)
                                            || HandleOperationMessage(message, localSocket, scaleOutBackend);
                                }
                            }
                            catch (NetMQException err)
                            {
                                logger.Error(string.Format($"{nameof(err.ErrorCode)}:{err.ErrorCode} " +
                                                           $"{nameof(err.Message)}:{err.Message} " +
                                                           $"Exception:{err}"));
                            }
                            catch (Exception err)
                            {
                                logger.Error(err);
                            }
                        }
                    }
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private bool HandleOperationMessage(Message message, ISocket localSocket, ISocket scaleOutBackend)
        {
            var messageHandlerIdentifier = CreateMessageHandlerIdentifier(message);

            return HandleMessageLocally(messageHandlerIdentifier, message, localSocket)
                   || ForwardMessageAway(messageHandlerIdentifier, message, scaleOutBackend)
                   || ProcessUnhandledMessage(message, messageHandlerIdentifier);
        }

        private bool HandleMessageLocally(MessageIdentifier messageIdentifier, Message message, ISocket localSocket)
        {
            var handlers = ((message.Distribution == DistributionPattern.Unicast)
                                ? new[] {internalRoutingTable.FindRoute(messageIdentifier)}
                                : internalRoutingTable.FindAllRoutes(messageIdentifier))
                .Where(h => h != null)
                .ToList();

            foreach (var handler in handlers)
            {
                message.SetSocketIdentity(handler.Identity);
                try
                {
                    localSocket.SendMessage(message);
                    messageTracer.RoutedToLocalActor(message);
                }
                catch (HostUnreachableException err)
                {
                    var removedHandlerIdentifiers = internalRoutingTable.RemoveActorHostRoute(handler);
                    if (removedHandlerIdentifiers.Any())
                    {
                        clusterMonitor.UnregisterSelf(removedHandlerIdentifiers);
                    }
                    logger.Error(err);
                }
            }

            return handlers.Any();
        }

        private bool ForwardMessageAway(MessageIdentifier messageIdentifier, Message message, ISocket scaleOutBackend)
        {
            var handlers = ((message.Distribution == DistributionPattern.Unicast)
                                ? new[] {externalRoutingTable.FindRoute(messageIdentifier)}
                                : (MessageCameFromLocalActor(message)
                                       ? externalRoutingTable.FindAllRoutes(messageIdentifier)
                                       : Enumerable.Empty<Node>()))
                .Where(h => h != null)
                .ToList();

            foreach (var handler in handlers)
            {
                try
                {
                    message.SetSocketIdentity(handler.SocketIdentity);
                    message.PushRouterAddress(routerConfiguration.ScaleOutAddress);
                    scaleOutBackend.SendMessage(message);

                    messageTracer.ForwardedToOtherNode(message);
                }
                catch (HostUnreachableException err)
                {
                    externalRoutingTable.RemoveNodeRoute(new SocketIdentifier(handler.SocketIdentity));
                    scaleOutBackend.Disconnect(handler.Uri);
                    logger.Error(err);
                }
            }

            return handlers.Any();
        }

        private bool ProcessUnhandledMessage(Message message, MessageIdentifier messageIdentifier)
        {
            clusterMonitor.DiscoverMessageRoute(messageIdentifier);

            if (MessageCameFromOtherNode(message))
            {
                clusterMonitor.UnregisterSelf(new[] {messageIdentifier});

                if (message.Distribution == DistributionPattern.Broadcast)
                {
                    logger.Warn("Broadcast message: " +
                                $"{nameof(message.Version)}:{message.Version.GetString()} " +
                                $"{nameof(message.Identity)}:{message.Identity.GetString()} " +
                                "didn't find any local handler and was not forwarded.");
                }
            }
            else
            {
                logger.Warn("Handler not found: " +
                            $"{nameof(messageIdentifier.Version)}:{messageIdentifier.Version.GetString()} " +
                            $"{nameof(messageIdentifier.Identity)}:{messageIdentifier.Identity.GetString()}");
            }

            return true;
        }

        private bool MessageCameFromLocalActor(Message message)
        {
            return !message.GetMessageHops().Any();
        }

        private bool MessageCameFromOtherNode(Message message)
        {
            return !MessageCameFromLocalActor(message);
        }

        private ISocket CreateScaleOutBackendSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            foreach (var peer in clusterMonitor.GetClusterMembers())
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
            => RegisterInternalMessageRoutes(message)
               || RegisterExternalRoutes(message, scaleOutBackend)
               || RequestRoutesRegistration(message)
               || DiscoverMessageRoute(message)
               || UnregisterRoute(message, scaleOutBackend)
               || UnregisterMessageRoute(message);

        private bool UnregisterMessageRoute(IMessage message)
        {
            var shouldHandle = IsUnregisterMessageRouting(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<UnregisterMessageRouteMessage>();
                externalRoutingTable.RemoveMessageRoute(payload
                                                            .MessageContracts
                                                            .Select(mh => new MessageIdentifier(mh.Version, mh.Identity)),
                                                        new SocketIdentifier(payload.SocketIdentity));
            }

            return shouldHandle;
        }

        private bool UnregisterRoute(IMessage message, ISocket scaleOutBackend)
        {
            var shouldHandle = IsUnregisterRouting(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<UnregisterNodeMessageRouteMessage>();
                externalRoutingTable.RemoveNodeRoute(new SocketIdentifier(payload.SocketIdentity));
                try
                {
                    scaleOutBackend.Disconnect(new Uri(payload.Uri));
                }
                catch (EndpointNotFoundException)
                {
                }
            }

            return shouldHandle;
        }

        private bool RequestRoutesRegistration(IMessage message)
        {
            var shouldHandle = IsRoutesRequest(message);
            if (shouldHandle)
            {
                var messageIdentifiers = internalRoutingTable.GetMessageIdentifiers();
                if (messageIdentifiers.Any())
                {
                    clusterMonitor.RegisterSelf(messageIdentifiers);
                }
            }

            return shouldHandle;
        }

        private bool DiscoverMessageRoute(IMessage message)
        {
            var shouldHandle = IsDiscoverMessageRouteRequest(message);
            if (shouldHandle)
            {
                var messageContract = message.GetPayload<DiscoverMessageRouteMessage>().MessageContract;
                var messageIdentifier = new MessageIdentifier(messageContract.Version, messageContract.Identity);
                if (internalRoutingTable.CanRouteMessage(messageIdentifier))
                {
                    clusterMonitor.RegisterSelf(new[] {messageIdentifier});
                }
            }

            return shouldHandle;
        }

        private bool RegisterExternalRoutes(IMessage message, ISocket scaleOutBackend)
        {
            var shouldHandle = IsExternalRouteRegistration(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<RegisterExternalMessageRouteMessage>();

                var handlerSocketIdentifier = new SocketIdentifier(payload.SocketIdentity);
                var uri = new Uri(payload.Uri);

                foreach (var registration in payload.MessageContracts)
                {
                    try
                    {
                        var messageHandlerIdentifier = new MessageIdentifier(registration.Version, registration.Identity);
                        externalRoutingTable.AddMessageRoute(messageHandlerIdentifier, handlerSocketIdentifier, uri);
                        scaleOutBackend.Connect(uri);
                    }
                    catch (Exception err)
                    {
                        logger.Error(err);
                    }
                }
            }

            return shouldHandle;
        }

        private static bool IsRoutesRequest(IMessage message)
            => Unsafe.Equals(RequestClusterMessageRoutesMessage.MessageIdentity, message.Identity)
               || Unsafe.Equals(RequestNodeMessageRoutesMessage.MessageIdentity, message.Identity);

        private static bool IsExternalRouteRegistration(IMessage message)
            => Unsafe.Equals(RegisterExternalMessageRouteMessage.MessageIdentity, message.Identity);

        private static bool IsInternalMessageRoutingRegistration(IMessage message)
            => Unsafe.Equals(RegisterInternalMessageRouteMessage.MessageIdentity, message.Identity);

        private bool IsUnregisterRouting(IMessage message)
            => Unsafe.Equals(UnregisterNodeMessageRouteMessage.MessageIdentity, message.Identity);

        private bool IsUnregisterMessageRouting(IMessage message)
            => Unsafe.Equals(UnregisterMessageRouteMessage.MessageIdentity, message.Identity);

        private bool IsDiscoverMessageRouteRequest(IMessage message)
            => Unsafe.Equals(DiscoverMessageRouteMessage.MessageIdentity, message.Identity);

        private bool RegisterInternalMessageRoutes(IMessage message)
        {
            var shouldHandle = IsInternalMessageRoutingRegistration(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<RegisterInternalMessageRouteMessage>();
                var handlerSocketIdentifier = new SocketIdentifier(payload.SocketIdentity);

                var handlers = UpdateLocalRoutingTable(payload, handlerSocketIdentifier);

                clusterMonitor.RegisterSelf(handlers);
            }

            return shouldHandle;
        }

        private IEnumerable<MessageIdentifier> UpdateLocalRoutingTable(RegisterInternalMessageRouteMessage payload,
                                                                       SocketIdentifier socketIdentifier)
        {
            var handlers = new List<MessageIdentifier>();

            foreach (var registration in payload.MessageContracts)
            {
                try
                {
                    var messageIdentifier = new MessageIdentifier(registration.Version, registration.Identity);
                    internalRoutingTable.AddMessageRoute(messageIdentifier, socketIdentifier);
                    handlers.Add(messageIdentifier);
                }
                catch (Exception err)
                {
                    logger.Error(err);
                }
            }

            return handlers;
        }

        private static MessageIdentifier CreateMessageHandlerIdentifier(IMessage message)
            => message.ReceiverIdentity.IsSet()
                   ? new MessageIdentifier(message.ReceiverIdentity)
                   : new MessageIdentifier(message.Version, message.Identity);
    }
}