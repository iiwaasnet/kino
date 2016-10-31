using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using kino.Core.Sockets;
using NetMQ;

namespace kino.Core.Connectivity
{
    public partial class MessageRouter : IMessageRouter
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private Task localRouting;
        private Task scaleOutRouting;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly IRouterConfigurationManager routerConfigurationManager;
        private readonly ISocketFactory socketFactory;
        private readonly TaskCompletionSource<byte[]> localSocketIdentityPromise;
        private readonly IClusterMonitor clusterMonitor;
        private readonly ILogger logger;
        private readonly IEnumerable<IServiceMessageHandler> serviceMessageHandlers;
        private readonly ClusterMembershipConfiguration membershipConfiguration;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ISecurityProvider securityProvider;
        private readonly ILocalSocket<IMessage> localRouterSocket;
        private readonly ILocalReceivingSocket<InternalRouteRegistration> internalRegistrationsReceiver;
        private readonly InternalMessageRouteRegistrationHandler internalRegistrationHandler;
        private static readonly TimeSpan TerminationWaitTimeout = TimeSpan.FromSeconds(3);

        public MessageRouter(ISocketFactory socketFactory,
                             IInternalRoutingTable internalRoutingTable,
                             IExternalRoutingTable externalRoutingTable,
                             IRouterConfigurationManager routerConfigurationManager,
                             IClusterMonitorProvider clusterMonitorProvider,
                             IEnumerable<IServiceMessageHandler> serviceMessageHandlers,
                             ClusterMembershipConfiguration membershipConfiguration,
                             IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                             ISecurityProvider securityProvider,
                             ILocalSocket<IMessage> localRouterSocket,
                             ILocalReceivingSocket<InternalRouteRegistration> internalRegistrationsReceiver,
                             InternalMessageRouteRegistrationHandler internalRegistrationHandler,
                             ILogger logger)
        {
            this.logger = logger;
            this.socketFactory = socketFactory;
            localSocketIdentityPromise = new TaskCompletionSource<byte[]>();
            this.internalRoutingTable = internalRoutingTable;
            this.externalRoutingTable = externalRoutingTable;
            this.routerConfigurationManager = routerConfigurationManager;
            clusterMonitor = clusterMonitorProvider.GetClusterMonitor();
            this.serviceMessageHandlers = serviceMessageHandlers;
            this.membershipConfiguration = membershipConfiguration;
            this.performanceCounterManager = performanceCounterManager;
            this.securityProvider = securityProvider;
            this.localRouterSocket = localRouterSocket;
            this.internalRegistrationsReceiver = internalRegistrationsReceiver;
            this.internalRegistrationHandler = internalRegistrationHandler;
            cancellationTokenSource = new CancellationTokenSource();
        }

        public bool Start(TimeSpan startTimeout)
        {
            localRouting = Task.Factory.StartNew(_ => RouteLocalMessages(cancellationTokenSource.Token),
                                                 TaskCreationOptions.LongRunning);
            scaleOutRouting = membershipConfiguration.RunAsStandalone
                                  ? Task.CompletedTask
                                  : Task.Factory.StartNew(_ => RoutePeerMessages(cancellationTokenSource.Token),
                                                          TaskCreationOptions.LongRunning);
            return clusterMonitor.Start(startTimeout);
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            localRouting?.Wait(TerminationWaitTimeout);
            scaleOutRouting?.Wait(TerminationWaitTimeout);
            cancellationTokenSource.Dispose();
            clusterMonitor.Stop();
        }

        private void RoutePeerMessages(CancellationToken token)
        {
            try
            {
                using (var scaleOutFrontend = CreateScaleOutFrontendSocket())
                {
                    //var localSocketIdentity = localSocketIdentityPromise.Task.Result;

                    while (!token.IsCancellationRequested)
                    {
                        Message message = null;
                        try
                        {
                            message = (Message) scaleOutFrontend.ReceiveMessage(token);
                            if (message != null)
                            {
                                message.VerifySignature(securityProvider);

                                //message.SetSocketIdentity(localSocketIdentity);
                                //scaleOutFrontend.SendMessage(message);
                                localRouterSocket.Send(message);

                                ReceivedFromOtherNode(message);
                            }
                        }
                        catch (SecurityException err)
                        {
                            CallbackSecurityException(err, message);
                            logger.Error(err);
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

        private void RouteLocalMessages(CancellationToken token)
        {
            try
            {
                //TODO: Remove this call after refactoring: no need for local socket identity any more
                routerConfigurationManager.SetMessageRouterConfigurationActive();
                //using (var localSocket = CreateRouterSocket())
                //{
                //    localSocketIdentityPromise.SetResult(localSocket.GetIdentity());

                using (var scaleOutBackend = CreateScaleOutBackendSocket())
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var receiverId = WaitHandle.WaitAny(new[]
                                                                {
                                                                    localRouterSocket.CanReceive(),
                                                                    internalRegistrationsReceiver.CanReceive(),
                                                                    token.WaitHandle
                                                                });
                            if (receiverId == 0)
                            {
                                var message = (Message) localRouterSocket.TryReceive();
                                if (message != null)
                                {
                                    var _ = TryHandleServiceMessage(message, scaleOutBackend)
                                            || HandleOperationMessage(message, scaleOutBackend);
                                }
                            }
                            if (receiverId == 1)
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
                //}
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
                //message.SetSocketIdentity(handler.Identity);
                try
                {
                    handler.Send(message);
                    //localSocket.SendMessage(message);
                    RoutedToLocalActor(message);
                }
                catch (HostUnreachableException err)
                {
                    var removedHandlerIdentifiers = internalRoutingTable.RemoveActorHostRoute(handler);
                    if (removedHandlerIdentifiers.Any())
                    {
                        clusterMonitor.UnregisterSelf(removedHandlerIdentifiers.Select(hi => hi.Identifier));
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

            var routerConfiguration = routerConfigurationManager.GetRouterConfiguration();
            foreach (var route in routes)
            {
                try
                {
                    if (!route.Connected)
                    {
                        scaleOutBackend.Connect(route.Node.Uri);
                        route.Connected = true;
                        routerConfiguration.ConnectionEstablishWaitTime.Sleep();
                    }

                    message.SetSocketIdentity(route.Node.SocketIdentity);
                    message.AddHop();
                    message.PushRouterAddress(routerConfigurationManager.GetScaleOutAddress());

                    message.SignMessage(securityProvider);

                    scaleOutBackend.SendMessage(message);

                    ForwardedToOtherNode(message);
                }
                catch (HostUnreachableException err)
                {
                    var unregMessage = new UnregisterUnreachableNodeMessage
                                       {
                                           SocketIdentity = route.Node.SocketIdentity,
                                           Uri = route.Node.Uri.ToSocketAddress()
                                       };
                    TryHandleServiceMessage(Message.Create(unregMessage), scaleOutBackend);
                    logger.Error(err);
                }
            }

            return routes.Any();
        }

        private bool ProcessUnhandledMessage(Message message, Identifier messageIdentifier)
        {
            clusterMonitor.DiscoverMessageRoute(messageIdentifier);

            if (MessageCameFromOtherNode(message))
            {
                clusterMonitor.UnregisterSelf(new[] {messageIdentifier});

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

        private bool MessageCameFromLocalActor(Message message)
            => message.Hops == 0;

        private bool MessageCameFromOtherNode(Message message)
            => !MessageCameFromLocalActor(message);

        private ISocket CreateScaleOutBackendSocket()
        {
            var socket = socketFactory.CreateRouterSocket();
            socket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.MessageRouterScaleoutBackendSocketSendRate);

            foreach (var peer in clusterMonitor.GetClusterMembers())
            {
                socket.Connect(peer.Uri);
            }

            return socket;
        }

        private ISocket CreateScaleOutFrontendSocket()
        {
            var routerConfiguration = routerConfigurationManager.GetRouterConfiguration();

            var socket = socketFactory.CreateRouterSocket();
            foreach (var scaleOutAddress in routerConfigurationManager.GetScaleOutAddressRange())
            {
                try
                {
                    socket.SetIdentity(scaleOutAddress.Identity);
                    socket.SetMandatoryRouting();
                    socket.SetReceiveHighWaterMark(GetScaleOutReceiveMessageQueueLength(routerConfiguration));
                    socket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.MessageRouterScaleoutFrontendSocketSendRate);
                    socket.ReceiveRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.MessageRouterScaleoutFrontendSocketReceiveRate);

                    socket.Bind(scaleOutAddress.Uri);
                    //SocketHelper.SafeConnect(() => socket.Connect(routerConfiguration.RouterAddress.Uri));
                    routerConfigurationManager.SetActiveScaleOutAddress(scaleOutAddress);

                    logger.Info($"MessageRouter started at Uri:{scaleOutAddress.Uri.ToSocketAddress()} " +
                                $"Identity:{scaleOutAddress.Identity.GetAnyString()}");

                    return socket;
                }
                catch
                {
                    logger.Info($"Failed to bind to {scaleOutAddress.Uri.ToSocketAddress()}, retrying with next endpoint...");
                }
            }

            throw new Exception($"Failed to bind to any of the configured ScaleOut endpoints!");
        }

        private int GetScaleOutReceiveMessageQueueLength(RouterConfiguration config)
        {
            var hwm = config.ScaleOutReceiveMessageQueueLength;
            var internalSocketsHWM = socketFactory.GetSocketDefaultConfiguration().ReceivingHighWatermark;

            if (hwm == 0 || hwm > internalSocketsHWM)
            {
                logger.Warn($"ScaleOutReceiveMessageQueueLength ({hwm}) cannot be greater, than internal ReceivingHighWatermark ({internalSocketsHWM}). " +
                            $"Current value of ScaleOutReceiveMessageQueueLength will be set to {internalSocketsHWM}.");
                hwm = internalSocketsHWM;
            }

            return hwm;
        }

        //private ISocket CreateRouterSocket()
        //{
        //    var routerConfiguration = routerConfigurationManager.GetInactiveRouterConfiguration();
        //    var socket = socketFactory.CreateRouterSocket();
        //    socket.SetMandatoryRouting();
        //    socket.SetIdentity(routerConfiguration.RouterAddress.Identity);
        //    socket.ReceiveRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.MessageRouterSocketReceiveRate);
        //    socket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.MessageRouterSocketSendRate);
        //    socket.Bind(routerConfiguration.RouterAddress.Uri);
        //    routerConfigurationManager.SetMessageRouterConfigurationActive();

        //    return socket;
        //}

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

        private void CallbackSecurityException(Exception err, Message messageIn)
        {
            var messageOut = Message.Create(new ExceptionMessage
                                            {
                                                Exception = err,
                                                StackTrace = err.StackTrace
                                            },
                                            securityProvider.GetDomain(KinoMessages.Exception.Identity))
                                    .As<Message>();
            messageOut.RegisterCallbackPoint(messageIn.CallbackReceiverIdentity, messageIn.CallbackPoint, messageIn.CallbackKey);
            messageOut.SetCorrelationId(messageIn.CorrelationId);
            messageOut.CopyMessageRouting(messageIn.GetMessageRouting());
            messageOut.TraceOptions |= messageIn.TraceOptions;
            //messageOut.SetSocketIdentity(localSocketIdentity);

            localRouterSocket.Send(messageOut);
        }
    }
}