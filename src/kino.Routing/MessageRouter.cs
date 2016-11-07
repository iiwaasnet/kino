using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;
using NetMQ;

namespace kino.Routing
{
    public partial class MessageRouter : IMessageRouter
    {
        private CancellationTokenSource cancellationTokenSource;
        private Task localRouting;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly IScaleOutConfigurationProvider scaleOutConfigurationProvider;
        private readonly IClusterConnectivity clusterConnectivity;
        private readonly ISocketFactory socketFactory;
        private readonly ILogger logger;
        private readonly IEnumerable<IServiceMessageHandler> serviceMessageHandlers;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ISecurityProvider securityProvider;
        private readonly ILocalSocket<IMessage> localRouterSocket;
        private readonly ILocalReceivingSocket<InternalRouteRegistration> internalRegistrationsReceiver;
        private readonly InternalMessageRouteRegistrationHandler internalRegistrationHandler;
        private static readonly TimeSpan TerminationWaitTimeout = TimeSpan.FromSeconds(3);

        public MessageRouter(ISocketFactory socketFactory,
                             IInternalRoutingTable internalRoutingTable,
                             IExternalRoutingTable externalRoutingTable,
                             IScaleOutConfigurationProvider scaleOutConfigurationProvider,
                             IClusterConnectivity clusterConnectivity,
                             IEnumerable<IServiceMessageHandler> serviceMessageHandlers,
                             IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                             ISecurityProvider securityProvider,
                             ILocalSocket<IMessage> localRouterSocket,
                             ILocalReceivingSocket<InternalRouteRegistration> internalRegistrationsReceiver,
                             InternalMessageRouteRegistrationHandler internalRegistrationHandler,
                             ILogger logger)
        {
            this.logger = logger;
            this.socketFactory = socketFactory;
            this.internalRoutingTable = internalRoutingTable;
            this.externalRoutingTable = externalRoutingTable;
            this.scaleOutConfigurationProvider = scaleOutConfigurationProvider;
            this.clusterConnectivity = clusterConnectivity;
            this.serviceMessageHandlers = serviceMessageHandlers;
            this.performanceCounterManager = performanceCounterManager;
            this.securityProvider = securityProvider;
            this.localRouterSocket = localRouterSocket;
            this.internalRegistrationsReceiver = internalRegistrationsReceiver;
            this.internalRegistrationHandler = internalRegistrationHandler;
        }

        public void Start()
        {
            //TODO: Decide on how to handle start timeout
            cancellationTokenSource = new CancellationTokenSource();
            localRouting = Task.Factory.StartNew(_ => RouteLocalMessages(cancellationTokenSource.Token),
                                                 TaskCreationOptions.LongRunning);
            clusterConnectivity.StartClusterServices();
        }

        public void Stop()
        {
            clusterConnectivity.StopClusterServices();
            cancellationTokenSource?.Cancel();
            localRouting?.Wait(TerminationWaitTimeout);
            cancellationTokenSource?.Dispose();
        }

        private void RouteLocalMessages(CancellationToken token)
        {
            try
            {
                const int LocalRouterSocketId = 0;
                const int InternalRegistrationsReceiverId = 1;
                var waitHandles = new[]
                                  {
                                      localRouterSocket.CanReceive(),
                                      internalRegistrationsReceiver.CanReceive(),
                                      token.WaitHandle
                                  };
                using (var scaleOutBackend = CreateScaleOutBackendSocket())
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var receiverId = WaitHandle.WaitAny(waitHandles);
                            if (receiverId == LocalRouterSocketId)
                            {
                                var message = (Message) localRouterSocket.TryReceive();
                                if (message != null)
                                {
                                    var _ = TryHandleServiceMessage(message, scaleOutBackend)
                                            || HandleOperationMessage(message, scaleOutBackend);
                                }
                            }
                            if (receiverId == InternalRegistrationsReceiverId)
                            {
                                var registration = internalRegistrationsReceiver.TryReceive();
                                if (registration != null)
                                {
                                    internalRegistrationHandler.Handle(registration);
                                }
                            }
                        }
                        catch (NetMQException err)
                        {
                            logger.Error($"{nameof(err.ErrorCode)}:{err.ErrorCode} " +
                                         $"{nameof(err.Message)}:{err.Message} " +
                                         $"Exception:{err}");
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

        private bool HandleOperationMessage(Message message, ISocket scaleOutBackend)
        {
            var messageHandlerIdentifier = CreateMessageHandlerIdentifier(message);

            var handled = !message.ReceiverNodeSet()
                          && HandleMessageLocally(messageHandlerIdentifier, message);
            if (!handled || message.Distribution == DistributionPattern.Broadcast)
            {
                handled = ForwardMessageAway(messageHandlerIdentifier, message, scaleOutBackend) || handled;
            }

            return handled || ProcessUnhandledMessage(message, messageHandlerIdentifier);
        }

        private bool HandleMessageLocally(Identifier messageIdentifier, Message message)
        {
            var handlers = (message.Distribution == DistributionPattern.Unicast
                                ? new[] {internalRoutingTable.FindRoute(messageIdentifier)}
                                : internalRoutingTable.FindAllRoutes(messageIdentifier))
                .Where(h => h != null)
                .ToList();

            foreach (var handler in handlers)
            {
                try
                {
                    message = MessageCameFromLocalActor(message)
                                  ? message.Clone().As<Message>()
                                  : message;

                    handler.Send(message);
                    RoutedToLocalActor(message);
                }
                catch (HostUnreachableException err)
                {
                    var removedHandlerIdentifiers = internalRoutingTable.RemoveActorHostRoute(handler);
                    if (removedHandlerIdentifiers.Any())
                    {
                        clusterConnectivity.UnregisterSelf(removedHandlerIdentifiers.Select(hi => hi.Identifier));
                    }
                    logger.Error(err);
                }
            }

            return handlers.Any();
        }

        private bool ForwardMessageAway(Identifier messageIdentifier, Message message, ISocket scaleOutBackend)
        {
            var receiverNodeIdentity = message.PopReceiverNode();
            var routes = (message.Distribution == DistributionPattern.Unicast
                              ? new[] {externalRoutingTable.FindRoute(messageIdentifier, receiverNodeIdentity)}
                              : (MessageCameFromLocalActor(message)
                                     ? externalRoutingTable.FindAllRoutes(messageIdentifier)
                                     : Enumerable.Empty<PeerConnection>()))
                .Where(h => h != null)
                .ToList();

            var routerConfiguration = scaleOutConfigurationProvider.GetRouterConfiguration();
            foreach (var route in routes)
            {
                try
                {
                    if (!route.Connected)
                    {
                        scaleOutBackend.Connect(route.Node.Uri);
                        route.Connected = true;
                        clusterConnectivity.StartPeerMonitoring(new Node(route.Node.Uri, route.Node.SocketIdentity),
                                                                route.Health);
                        routerConfiguration.ConnectionEstablishWaitTime.Sleep();
                    }

                    message.SetSocketIdentity(route.Node.SocketIdentity);
                    message.AddHop();
                    message.PushRouterAddress(scaleOutConfigurationProvider.GetScaleOutAddress());

                    message.SignMessage(securityProvider);

                    scaleOutBackend.SendMessage(message);

                    ForwardedToOtherNode(message);
                }
                catch (HostUnreachableException err)
                {
                    var unregMessage = new UnregisterUnreachableNodeMessage {SocketIdentity = route.Node.SocketIdentity};
                    TryHandleServiceMessage(Message.Create(unregMessage), scaleOutBackend);
                    logger.Error(err);
                }
            }

            return routes.Any();
        }

        private bool ProcessUnhandledMessage(IMessage message, Identifier messageIdentifier)
        {
            clusterConnectivity.DiscoverMessageRoute(messageIdentifier);

            if (MessageCameFromOtherNode(message))
            {
                clusterConnectivity.UnregisterSelf(new[] {messageIdentifier});

                if (message.Distribution == DistributionPattern.Broadcast)
                {
                    logger.Warn($"Broadcast message: {messageIdentifier} didn't find any local handler and was not forwarded.");
                }
            }
            else
            {
                logger.Warn($"Handler not found: {messageIdentifier}");
            }

            return true;
        }

        private static bool MessageCameFromLocalActor(IMessage message)
            => message.Hops == 0;

        private static bool MessageCameFromOtherNode(IMessage message)
            => !MessageCameFromLocalActor(message);

        private ISocket CreateScaleOutBackendSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            socket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.MessageRouterScaleoutBackendSocketSendRate);

            return socket;
        }

        private bool TryHandleServiceMessage(IMessage message, ISocket scaleOutBackend)
        {
            var handled = false;
            var enumerator = serviceMessageHandlers.GetEnumerator();
            while (enumerator.MoveNext() && !handled)
            {
                handled = enumerator.Current.Handle(message, scaleOutBackend);
            }

            return handled;
        }

        private static Identifier CreateMessageHandlerIdentifier(Message message)
            => message.ReceiverIdentity.IsSet()
                   ? (Identifier) new AnyIdentifier(message.ReceiverIdentity)
                   : (Identifier) new MessageIdentifier(message);
    }
}