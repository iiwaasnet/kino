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
        private readonly IClusterConfiguration clusterConfiguration;
        private readonly RouterConfiguration routerConfiguration;
        private readonly ILogger logger;
        private readonly IMessageTracer messageTracer;
        private static readonly TimeSpan TerminationWaitTimeout = TimeSpan.FromSeconds(3);

        public MessageRouter(ISocketFactory socketFactory,
                             IInternalRoutingTable internalRoutingTable,
                             IExternalRoutingTable externalRoutingTable,
                             IClusterConfiguration clusterConfiguration,
                             RouterConfiguration routerConfiguration,
                             IClusterMonitor clusterMonitor,
                             IMessageTracer messageTracer,
                             ILogger logger)
        {
            this.logger = logger;
            this.messageTracer = messageTracer;
            this.socketFactory = socketFactory;
            this.clusterConfiguration = clusterConfiguration;
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
                    clusterMonitor.RequestMessageHandlersRouting();

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
                   || LogUnhandledMessage(message, messageHandlerIdentifier);
        }

        private bool HandleMessageLocally(MessageHandlerIdentifier messageHandlerIdentifier, Message message, ISocket localSocket)
        {
            var handlers = ((message.Distribution == DistributionPattern.Unicast)
                                ? new[] {internalRoutingTable.Pop(messageHandlerIdentifier)}
                                : internalRoutingTable.PopAll(messageHandlerIdentifier))
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
                    var removedHandlerIdentifiers = internalRoutingTable.Remove(handler);
                    if (removedHandlerIdentifiers.Any())
                    {
                        clusterMonitor.UnregisterSelf(removedHandlerIdentifiers);
                    }
                    logger.Error(err);
                }
            }

            return handlers.Any();
        }

        private bool ForwardMessageAway(MessageHandlerIdentifier messageHandlerIdentifier, Message message, ISocket scaleOutBackend)
        {
            var handlers = ((message.Distribution == DistributionPattern.Unicast)
                                ? new[] {externalRoutingTable.Pop(messageHandlerIdentifier)}
                                : (MessageCameFromLocalActor(message)
                                       ? externalRoutingTable.PopAll(messageHandlerIdentifier)
                                       : Enumerable.Empty<SocketIdentifier>()))
                .Where(h => h != null)
                .ToList();

            foreach (var handler in handlers)
            {
                message.SetSocketIdentity(handler.Identity);
                message.PushRouterAddress(routerConfiguration.ScaleOutAddress);
                scaleOutBackend.SendMessage(message);

                messageTracer.ForwardedToOtherNode(message);
            }

            return handlers.Any();
        }

        private bool LogUnhandledMessage(Message message, MessageHandlerIdentifier messageHandlerIdentifier)
        {
            if (!MessageCameFromLocalActor(message) && message.Distribution == DistributionPattern.Broadcast)
            {
                logger.Warn("Broadcast message: " +
                            $"{nameof(message.Version)}:{message.Version.GetString()} " +
                            $"{nameof(message.Identity)}:{message.Identity.GetString()} " +
                            "didn't find any local handler and was not forwarded.");
            }
            else
            {
                logger.Warn("Handler not found: " +
                            $"{nameof(messageHandlerIdentifier.Version)}:{messageHandlerIdentifier.Version.GetString()} " +
                            $"{nameof(messageHandlerIdentifier.Identity)}:{messageHandlerIdentifier.Identity.GetString()}");
            }

            return true;
        }

        private bool MessageCameFromLocalActor(Message message)
        {
            return !message.GetMessageHops().Any();
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
            => RegisterInternalMessageRouting(message)
               || RegisterExternalRouting(message, scaleOutBackend)
               || RequestRoutingRegistration(message)
               || UnregisterRouting(message, scaleOutBackend)
               || UnregisterMessageRouting(message);

        private bool UnregisterMessageRouting(IMessage message)
        {
            var shouldHandle = IsUnregisterMessageRouting(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<UnregisterMessageRoutingMessage>();
                externalRoutingTable.RemoveMessageRoute(payload
                                                            .MessageHandlers
                                                            .Select(mh => new MessageHandlerIdentifier(mh.Version, mh.Identity)),
                                                        new SocketIdentifier(payload.SocketIdentity));
            }

            return shouldHandle;
        }

        private bool UnregisterRouting(IMessage message, ISocket scaleOutBackend)
        {
            var shouldHandle = IsUnregisterRouting(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<UnregisterRoutingMessage>();
                externalRoutingTable.RemoveRoute(new SocketIdentifier(payload.SocketIdentity));
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

        private bool RequestRoutingRegistration(IMessage message)
        {
            var shouldHandle = IsRoutingRequest(message);
            if (shouldHandle)
            {
                var messageIdentifiers = internalRoutingTable.GetMessageHandlerIdentifiers();
                if (messageIdentifiers.Any())
                {
                    clusterMonitor.RegisterSelf(messageIdentifiers);
                }
            }

            return shouldHandle;
        }

        private bool RegisterExternalRouting(IMessage message, ISocket scaleOutBackend)
        {
            var shouldHandle = IsExternalRouteRegistration(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<RegisterMessageRoutingMessage>();

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
                        logger.Error(err);
                    }
                }
            }

            return shouldHandle;
        }

        private static bool IsRoutingRequest(IMessage message)
            => Unsafe.Equals(RequestAllMessageRoutingMessage.MessageIdentity, message.Identity)
               || Unsafe.Equals(RequestNodeMessageRoutingMessage.MessageIdentity, message.Identity);

        private static bool IsExternalRouteRegistration(IMessage message)
            => Unsafe.Equals(RegisterMessageRoutingMessage.MessageIdentity, message.Identity);

        private static bool IsInternalMessageRoutingRegistration(IMessage message)
            => Unsafe.Equals(RegisterMessageHandlersMessage.MessageIdentity, message.Identity);

        private bool IsUnregisterRouting(IMessage message)
            => Unsafe.Equals(UnregisterRoutingMessage.MessageIdentity, message.Identity);

        private bool IsUnregisterMessageRouting(IMessage message)
            => Unsafe.Equals(UnregisterMessageRoutingMessage.MessageIdentity, message.Identity);

        private bool RegisterInternalMessageRouting(IMessage message)
        {
            var shouldHandle = IsInternalMessageRoutingRegistration(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<RegisterMessageHandlersMessage>();
                var handlerSocketIdentifier = new SocketIdentifier(payload.SocketIdentity);

                var handlers = UpdateLocalRoutingTable(payload, handlerSocketIdentifier);

                clusterMonitor.RegisterSelf(handlers);
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
                    logger.Error(err);
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