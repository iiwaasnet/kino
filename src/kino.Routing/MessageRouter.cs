using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        private Thread localRouting;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly IScaleOutConfigurationProvider scaleOutConfigurationProvider;
        private readonly IClusterServices clusterServices;
        private readonly IServiceMessageHandlerRegistry serviceMessageHandlerRegistry;
        private readonly ISocketFactory socketFactory;
        private readonly ILogger logger;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ISecurityProvider securityProvider;
        private readonly ILocalSocket<IMessage> localRouterSocket;
        private readonly ILocalReceivingSocket<InternalRouteRegistration> internalRegistrationsReceiver;
        private readonly IInternalMessageRouteRegistrationHandler internalRegistrationHandler;
        private readonly IRoundRobinDestinationList roundRobinDestinationList;
        private byte[] thisNodeIdentity;
        private bool isStarted;

        public MessageRouter(ISocketFactory socketFactory,
                             ILocalSocketFactory localSocketFactory,
                             IInternalRoutingTable internalRoutingTable,
                             IExternalRoutingTable externalRoutingTable,
                             IScaleOutConfigurationProvider scaleOutConfigurationProvider,
                             IClusterServices clusterServices,
                             IServiceMessageHandlerRegistry serviceMessageHandlerRegistry,
                             IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                             ISecurityProvider securityProvider,
                             IInternalMessageRouteRegistrationHandler internalRegistrationHandler,
                             IRoundRobinDestinationList roundRobinDestinationList,
                             ILogger logger)
        {
            this.logger = logger;
            this.socketFactory = socketFactory;
            this.internalRoutingTable = internalRoutingTable;
            this.externalRoutingTable = externalRoutingTable;
            this.scaleOutConfigurationProvider = scaleOutConfigurationProvider;
            this.clusterServices = clusterServices;
            this.serviceMessageHandlerRegistry = serviceMessageHandlerRegistry;
            this.performanceCounterManager = performanceCounterManager;
            this.securityProvider = securityProvider;
            localRouterSocket = localSocketFactory.CreateNamed<IMessage>(NamedSockets.RouterLocalSocket);
            internalRegistrationsReceiver = localSocketFactory.CreateNamed<InternalRouteRegistration>(NamedSockets.InternalRegistrationSocket);
            localRouterSocket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.MessageRouterLocalSocketSendRate);
            localRouterSocket.ReceiveRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.MessageRouterLocalSocketReceiveRate);
            this.internalRegistrationHandler = internalRegistrationHandler;
            this.roundRobinDestinationList = roundRobinDestinationList;
        }

        public void Start()
        {
            if (!isStarted)
            {
                using (var barrier = new Barrier(2))
                {
                    //TODO: Decide on how to handle start timeout
                    cancellationTokenSource = new CancellationTokenSource();
                    clusterServices.StartClusterServices();
                    localRouting = new Thread(() => RouteLocalMessages(cancellationTokenSource.Token, barrier))
                                   {
                                       IsBackground = true
                                   };
                    localRouting.Start();

                    barrier.SignalAndWait(cancellationTokenSource.Token);
                    isStarted = true;
                }
            }
        }

        public void Stop()
        {
            clusterServices.StopClusterServices();
            cancellationTokenSource?.Cancel();
            localRouting?.Join();
            cancellationTokenSource?.Dispose();
            isStarted = false;
        }

        private byte[] GetBlockingReceiverNodeIdentity()
            => scaleOutConfigurationProvider.GetScaleOutAddress().Identity;

        private void RouteLocalMessages(CancellationToken token, Barrier barrier)
        {
            const int cancellationToken = 2;
            try
            {
                thisNodeIdentity = GetBlockingReceiverNodeIdentity();

                var waitHandles = new[]
                                  {
                                      localRouterSocket.CanReceive(),
                                      internalRegistrationsReceiver.CanReceive(),
                                      token.WaitHandle
                                  };
                using (var scaleOutBackend = CreateScaleOutBackendSocket())
                {
                    barrier.SignalAndWait(token);
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var receiverId = WaitHandle.WaitAny(waitHandles);
                            if (receiverId != cancellationToken)
                            {
                                var message = localRouterSocket.TryReceive().As<Message>();
                                if (message != null)
                                {
                                    var _ = TryHandleServiceMessage(message, scaleOutBackend)
                                         || HandleOperationMessage(message, scaleOutBackend);
                                }

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
            var lookupRequest = new ExternalRouteLookupRequest
                                {
                                    ReceiverIdentity = new ReceiverIdentifier(message.ReceiverIdentity),
                                    Message = new MessageIdentifier(message),
                                    Distribution = message.Distribution,
                                    ReceiverNodeIdentity = new ReceiverIdentifier(message.ReceiverNodeIdentity ?? IdentityExtensions.Empty)
                                };

            var handled = message.ReceiverNodeIdentity.IsSet()
                              ? SendToExactReceiver(message, scaleOutBackend, lookupRequest)
                              : SelectReceiverAndSend(message, scaleOutBackend, lookupRequest);

            return handled || ProcessUnhandledMessage(message, lookupRequest);
        }

        private bool SendToExactReceiver(Message message, ISocket scaleOutBackend, ExternalRouteLookupRequest lookupRequest)
        {
            if (Unsafe.ArraysEqual(message.ReceiverNodeIdentity, thisNodeIdentity))
            {
                var localDestinations = internalRoutingTable.FindRoutes(lookupRequest);
                return SendMessageLocally(localDestinations, message);
            }

            var remoteDestinations = externalRoutingTable.FindRoutes(lookupRequest);
            return SendMessageAway(remoteDestinations, message, scaleOutBackend);
        }

        private bool SelectReceiverAndSend(Message message, ISocket scaleOutBackend, ExternalRouteLookupRequest lookupRequest)
        {
            var handled = false;

            if (message.Distribution == DistributionPattern.Broadcast)
            {
                handled = SendMessageLocally(internalRoutingTable.FindRoutes(lookupRequest), message);
                handled = MessageCameFromLocalActor(message)
                       && SendMessageAway(externalRoutingTable.FindRoutes(lookupRequest), message, scaleOutBackend)
                       || handled;
            }
            else
            {
                var localDestinations = internalRoutingTable.FindRoutes(lookupRequest);
                var remoteDestinations = externalRoutingTable.FindRoutes(lookupRequest);
                var local = localDestinations.FirstOrDefault()?.As<IDestination>();
                var remote = remoteDestinations.FirstOrDefault()?.Node.As<IDestination>();

                var destination = (local != null && remote != null)
                                      ? roundRobinDestinationList.SelectNextDestination(local, remote)
                                      : (local ?? remote);
                if (destination != null)
                {
                    if (MessageCameFromOtherNode(message) || destination.Equals(local))
                    {
                        handled = SendMessageLocally(localDestinations, message);
                    }

                    if (!handled)
                    {
                        handled = SendMessageAway(remoteDestinations, message, scaleOutBackend);
                    }
                }
            }

            return handled;
        }

        private bool SendMessageLocally(IEnumerable<ILocalSendingSocket<IMessage>> destinations, Message message)
        {
            foreach (var destination in destinations)
            {
                try
                {
                    message = MessageCameFromLocalActor(message)
                                  ? message.Clone()
                                  : message;

                    message.SetSocketIdentity(destination.As<ILocalSocket<IMessage>>().GetIdentity().Identity);
                    destination.Send(message);
                    RoutedToLocalActor(message);
                }
                catch (HostUnreachableException err)
                {
                    //TODO: HostUnreachableException will never happen here, hence NetMQ sockets are not used
                    // ILocalSocketShould throw similar exception, if no one is reading messages from the socket,
                    // which should be a trigger for deletion of the ActorHost
                    // When change is done, cover with unitests
                    var removedRoutes = internalRoutingTable.RemoveReceiverRoute(destination)
                                                            .Select(rr => new Cluster.MessageRoute
                                                                          {
                                                                              Receiver = rr.Receiver,
                                                                              Message = rr.Message
                                                                          });
                    if (removedRoutes.Any())
                    {
                        clusterServices.GetClusterMonitor().UnregisterSelf(removedRoutes);
                    }

                    logger.Error(err);
                }
            }

            return destinations.Any();
        }

        private bool SendMessageAway(IEnumerable<PeerConnection> routes, Message message, ISocket scaleOutBackend)
        {
            foreach (var route in routes)
            {
                try
                {
                    if (!route.Connected)
                    {
                        scaleOutBackend.Connect(route.Node.Uri, waitUntilConnected: true);
                        route.Connected = true;
                        clusterServices.GetClusterHealthMonitor()
                                       .StartPeerMonitoring(new Node(route.Node.Uri, route.Node.SocketIdentity), route.Health);
                    }

                    message.SetSocketIdentity(route.Node.SocketIdentity);
                    message.AddHop();
                    message.PushRouterAddress(scaleOutConfigurationProvider.GetScaleOutAddress());

                    message.SignMessage(securityProvider);

                    scaleOutBackend.Send(message);

                    ForwardedToOtherNode(message);
                }
                catch (TimeoutException err)
                {
                    clusterServices.GetClusterHealthMonitor()
                                   .ScheduleConnectivityCheck(new ReceiverIdentifier(route.Node.SocketIdentity));
                    logger.Error(err);
                }
                catch (HostUnreachableException err)
                {
                    var unregMessage = new UnregisterUnreachableNodeMessage {ReceiverNodeIdentity = route.Node.SocketIdentity};
                    TryHandleServiceMessage(Message.Create(unregMessage).As<Message>(), scaleOutBackend);
                    logger.Error(err);
                }
            }

            return routes.Any();
        }

        private bool ProcessUnhandledMessage(IMessage message, ExternalRouteLookupRequest lookupRequest)
        {
            var messageRoute = new Cluster.MessageRoute
                               {
                                   Receiver = lookupRequest.ReceiverIdentity,
                                   Message = lookupRequest.Message
                               };
            clusterServices.GetClusterMonitor().DiscoverMessageRoute(messageRoute);

            if (MessageCameFromOtherNode(message))
            {
                clusterServices.GetClusterMonitor().UnregisterSelf(messageRoute.ToEnumerable());
            }

            var route = message.As<Message>().GetMessageRouting().FirstOrDefault();
            logger.Warn($"Route not found for Message:{lookupRequest.Message} lookup by " +
                        $"[{nameof(lookupRequest.ReceiverNodeIdentity)}:{lookupRequest.ReceiverNodeIdentity}]-" +
                        $"[{nameof(lookupRequest.ReceiverIdentity)}:{lookupRequest.ReceiverIdentity}]-" +
                        $"[{lookupRequest.Distribution}] " +
                        $"Sent by:[{route?.Identity.GetAnyString()}@{route?.Uri}]");

            return true;
        }

        private static bool MessageCameFromLocalActor(IMessage message)
            => message.Hops == 0;

        private static bool MessageCameFromOtherNode(IMessage message)
            => !MessageCameFromLocalActor(message);

        private ISocket CreateScaleOutBackendSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            socket.SetMandatoryRouting();
            socket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.MessageRouterScaleoutBackendSocketSendRate);

            return socket;
        }

        private bool TryHandleServiceMessage(Message message, ISocket scaleOutBackend)
        {
            var serviceMessageHandler = serviceMessageHandlerRegistry.GetMessageHandler(message);

            serviceMessageHandler?.Handle(message, scaleOutBackend);

            return serviceMessageHandler != null;
        }
    }
}