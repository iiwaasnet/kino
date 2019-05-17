using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using kino.Cluster.Kafka;
using kino.Connectivity;
using kino.Connectivity.Kafka;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Routing.Kafka.ServiceMessageHandlers;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;

namespace kino.Routing.Kafka
{
    public class KafkaMessageRouter : IMessageRouter
    {
        private CancellationTokenSource cancellationTokenSource;
        private Thread localRouting;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly IKafkaExternalRoutingTable externalRoutingTable;
        private readonly IKafkaScaleOutConfigurationProvider scaleOutConfigurationProvider;
        private readonly IKafkaClusterServices clusterServices;
        private readonly IKafkaServiceMessageHandlerRegistry serviceMessageHandlerRegistry;
        private readonly ILogger logger;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ISecurityProvider securityProvider;
        private readonly ILocalSocket<IMessage> localRouterSocket;
        private readonly ILocalReceivingSocket<InternalRouteRegistration> internalRegistrationsReceiver;
        private readonly IInternalMessageRouteRegistrationHandler internalRegistrationHandler;
        private readonly IRoundRobinDestinationList roundRobinDestinationList;
        private byte[] thisNodeIdentity;
        private bool isStarted;
        private readonly ISender sender;

        public KafkaMessageRouter(IKafkaConnectionFactory connectionFactory,
                                  ILocalSocketFactory localSocketFactory,
                                  IInternalRoutingTable internalRoutingTable,
                                  IKafkaExternalRoutingTable externalRoutingTable,
                                  IKafkaScaleOutConfigurationProvider scaleOutConfigurationProvider,
                                  IKafkaClusterServices clusterServices,
                                  IKafkaServiceMessageHandlerRegistry serviceMessageHandlerRegistry,
                                  IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                  ISecurityProvider securityProvider,
                                  IInternalMessageRouteRegistrationHandler internalRegistrationHandler,
                                  IRoundRobinDestinationList roundRobinDestinationList,
                                  ILogger logger)
        {
            this.logger = logger;
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
            sender = connectionFactory.CreateSender();
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

        private byte[] GetLocalNodeIdentity()
            => scaleOutConfigurationProvider.GetScaleOutAddress().Identity;

        private void RouteLocalMessages(CancellationToken token, Barrier barrier)
        {
            const int cancellationToken = 2;
            try
            {
                thisNodeIdentity = GetLocalNodeIdentity();

                var waitHandles = new[]
                                  {
                                      localRouterSocket.CanReceive(),
                                      internalRegistrationsReceiver.CanReceive(),
                                      token.WaitHandle
                                  };

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
                                var _ = TryHandleServiceMessage(message)
                                        || HandleOperationMessage(message);
                            }

                            var registration = internalRegistrationsReceiver.TryReceive();
                            if (registration != null)
                            {
                                internalRegistrationHandler.Handle(registration);
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        logger.Error(err);
                    }
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private bool HandleOperationMessage(Message message)
        {
            var lookupRequest = new ExternalRouteLookupRequest
                                {
                                    ReceiverIdentity = new ReceiverIdentifier(message.ReceiverIdentity),
                                    Message = new MessageIdentifier(message),
                                    Distribution = message.Distribution,
                                    ReceiverNodeIdentity = new ReceiverIdentifier(message.ReceiverNodeIdentity ?? IdentityExtensions.Empty)
                                };

            var handled = message.ReceiverNodeIdentity.IsSet()
                              ? SendToExactReceiver(message, lookupRequest)
                              : SelectReceiverAndSend(message, lookupRequest);

            return handled || ProcessUnhandledMessage(message, lookupRequest);
        }

        private bool SendToExactReceiver(Message message, ExternalRouteLookupRequest lookupRequest)
        {
            if (Unsafe.ArraysEqual(message.ReceiverNodeIdentity, thisNodeIdentity))
            {
                var localDestinations = internalRoutingTable.FindRoutes(lookupRequest);
                return SendMessageLocally(localDestinations, message);
            }

            var remoteDestinations = externalRoutingTable.FindRoutes(lookupRequest);
            return SendMessageAway(remoteDestinations, message);
        }

        private bool SelectReceiverAndSend(Message message, ExternalRouteLookupRequest lookupRequest)
        {
            var handled = false;

            if (message.Distribution == DistributionPattern.Broadcast)
            {
                handled = SendMessageLocally(internalRoutingTable.FindRoutes(lookupRequest), message);
                handled = MessageCameFromLocalActor(message)
                          && SendMessageAway(externalRoutingTable.FindRoutes(lookupRequest), message)
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
                        handled = SendMessageAway(remoteDestinations, message);
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
                catch (Exception err)
                {
                    logger.Error(err);
                }
            }

            return destinations.Any();
        }

        private bool SendMessageAway(IEnumerable<KafkaPeerConnection> routes, Message message)
        {
            foreach (var route in routes)
            {
                try
                {
                    if (!route.Connected)
                    {
                        sender.Connect(route.Node.BrokerName);
                        route.Connected = true;
                        clusterServices.GetClusterHealthMonitor()
                                       .StartPeerMonitoring(route.Node, route.Health);
                    }

                    message.SetSocketIdentity(route.Node.Identity);
                    message.AddHop();
                    var node = scaleOutConfigurationProvider.GetScaleOutAddress();
                    message.PushNodeAddress(new NodeAddress
                                            {
                                                Address = node.BrokerName,
                                                Identity = node.Identity
                                            });

                    message.SignMessage(securityProvider);

                    sender.Send(route.Node.BrokerName, SelectDestination(route.Node, message), message);

                    ForwardedToOtherNode(message);
                }
                catch (Exception err)
                {
                    logger.Error(err);
                }
            }

            return routes.Any();
        }

        private static string SelectDestination(KafkaNode node, Message message)
            => message.ReceiverNodeIdentity.IsSet() || message.Distribution == DistributionPattern.Broadcast
                   ? node.Topic
                   : node.Queue;

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
                        $"Sent by:[{route?.Identity.GetAnyString()}@{route?.Address}]");

            return true;
        }

        private static bool MessageCameFromLocalActor(IMessage message)
            => message.Hops == 0;

        private static bool MessageCameFromOtherNode(IMessage message)
            => !MessageCameFromLocalActor(message);

        private bool TryHandleServiceMessage(Message message)
        {
            var serviceMessageHandler = serviceMessageHandlerRegistry.GetMessageHandler(message);

            serviceMessageHandler?.Handle(message, sender);

            return serviceMessageHandler != null;
        }

        private void RoutedToLocalActor(Message message)
        {
            if ((message.TraceOptions & MessageTraceOptions.Routing) == MessageTraceOptions.Routing)
            {
                logger.Trace($"Message: {message} " +
                             $"routed to {nameof(message.SocketIdentity)}:{message.SocketIdentity.GetAnyString()}");
            }
        }

        private void ForwardedToOtherNode(Message message)
        {
            if ((message.TraceOptions & MessageTraceOptions.Routing) == MessageTraceOptions.Routing)
            {
                logger.Trace($"Message: {message} " +
                             $"forwarded to other node {nameof(message.SocketIdentity)}:{message.SocketIdentity.GetAnyString()}");
            }
        }
    }
}