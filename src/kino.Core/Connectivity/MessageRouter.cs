using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
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
        private readonly ISocketFactory socketFactory;
        private readonly TaskCompletionSource<byte[]> localSocketIdentityPromise;
        private readonly IClusterMonitor clusterMonitor;
        private readonly RouterConfiguration routerConfiguration;
        private readonly ILogger logger;
        private readonly IEnumerable<IServiceMessageHandler> serviceMessageHandlers;
        private readonly ClusterMembershipConfiguration membershipConfiguration;
        private static readonly TimeSpan TerminationWaitTimeout = TimeSpan.FromSeconds(3);

        public MessageRouter(ISocketFactory socketFactory,
                             IInternalRoutingTable internalRoutingTable,
                             IExternalRoutingTable externalRoutingTable,
                             RouterConfiguration routerConfiguration,
                             IClusterMonitor clusterMonitor,
                             IEnumerable<IServiceMessageHandler> serviceMessageHandlers,
                             ClusterMembershipConfiguration membershipConfiguration,
                             ILogger logger)
        {
            this.logger = logger;
            this.socketFactory = socketFactory;
            localSocketIdentityPromise = new TaskCompletionSource<byte[]>();
            this.internalRoutingTable = internalRoutingTable;
            this.externalRoutingTable = externalRoutingTable;
            this.clusterMonitor = clusterMonitor;
            this.routerConfiguration = SetDefaultsForMissingMembers(routerConfiguration);
            this.serviceMessageHandlers = serviceMessageHandlers;
            this.membershipConfiguration = membershipConfiguration;
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            localRouting = Task.Factory.StartNew(_ => RouteLocalMessages(cancellationTokenSource.Token),
                                                 TaskCreationOptions.LongRunning);
            scaleOutRouting = membershipConfiguration.RunAsStandalone
                                  ? Task.CompletedTask
                                  : Task.Factory.StartNew(_ => RoutePeerMessages(cancellationTokenSource.Token),
                                                          TaskCreationOptions.LongRunning);
            SocketHelper.SafeConnect(clusterMonitor.Start);
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            localRouting.Wait(TerminationWaitTimeout);
            scaleOutRouting.Wait(TerminationWaitTimeout);
            cancellationTokenSource.Dispose();
            clusterMonitor.Stop();
        }

        private void RoutePeerMessages(CancellationToken token)
        {
            try
            {
                using (var scaleOutFrontend = CreateScaleOutFrontendSocket())
                {
                    var localSocketIdentity = localSocketIdentityPromise.Task.Result;

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var message = (Message) scaleOutFrontend.ReceiveMessage(token);
                            if (message != null)
                            {
                                message.SetSocketIdentity(localSocketIdentity);
                                scaleOutFrontend.SendMessage(message);

                                ReceivedFromOtherNode(message);
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

        private void RouteLocalMessages(CancellationToken token)
        {
            try
            {
                using (var localSocket = CreateRouterSocket())
                {
                    localSocketIdentityPromise.SetResult(localSocket.GetIdentity());
                    clusterMonitor.RequestClusterRoutes();

                    using (var scaleOutBackend = CreateScaleOutBackendSocket())
                    {
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

            var handled = HandleMessageLocally(messageHandlerIdentifier, message, localSocket);
            if (!handled || message.Distribution == DistributionPattern.Broadcast)
            {
                handled = ForwardMessageAway(messageHandlerIdentifier, message, scaleOutBackend) || handled;
            }

            return handled || ProcessUnhandledMessage(message, messageHandlerIdentifier);
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
                    RoutedToLocalActor(message);
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
            var routes = ((message.Distribution == DistributionPattern.Unicast)
                              ? new[] {externalRoutingTable.FindRoute(messageIdentifier)}
                              : (MessageCameFromLocalActor(message)
                                     ? externalRoutingTable.FindAllRoutes(messageIdentifier)
                                     : Enumerable.Empty<PeerConnection>()))
                .Where(h => h != null)
                .ToList();

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
                    message.PushRouterAddress(routerConfiguration.ScaleOutAddress);

                    scaleOutBackend.SendMessage(message);

                    ForwardedToOtherNode(message);
                }
                catch (HostUnreachableException err)
                {
                    var unregMessage = new UnregisterNodeMessageRouteMessage
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
            => message.Hops == 0;

        private bool MessageCameFromOtherNode(Message message)
            => !MessageCameFromLocalActor(message);

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
            socket.SetReceiveHighWaterMark(GetScaleOutReceiveMessageQueueLength());
            SocketHelper.SafeConnect(() => socket.Connect(routerConfiguration.RouterAddress.Uri));
            socket.Bind(routerConfiguration.ScaleOutAddress.Uri);

            logger.Info($"MessageRouter started at Uri:{routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress()} " +
                        $"Identity:{routerConfiguration.ScaleOutAddress.Identity.GetString()}");

            return socket;
        }

        private int GetScaleOutReceiveMessageQueueLength()
        {
            var hwm = routerConfiguration.ScaleOutReceiveMessageQueueLength;
            var internalSocketsHWM = socketFactory.GetSocketDefaultConfiguration().ReceivingHighWatermark;

            if (hwm == 0 || hwm > internalSocketsHWM)
            {
                logger.Warn($"ScaleOutReceiveMessageQueueLength ({hwm}) cannot be greater, than internal ReceivingHighWatermark ({internalSocketsHWM}). " +
                            $"Current value of ScaleOutReceiveMessageQueueLength will be set to {internalSocketsHWM}.");
                hwm = internalSocketsHWM;
            }

            return hwm;
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
        {
            var handled = false;
            var enumerator = serviceMessageHandlers.GetEnumerator();
            while (enumerator.MoveNext() && !handled)
            {
                handled = enumerator.Current.Handle(message, scaleOutBackend);
            }

            return handled;
        }

        private static MessageIdentifier CreateMessageHandlerIdentifier(Message message)
            => message.ReceiverIdentity.IsSet()
                   ? new MessageIdentifier(message.ReceiverIdentity)
                   : new MessageIdentifier(message.Version, message.Identity);

        private RouterConfiguration SetDefaultsForMissingMembers(RouterConfiguration routerConfiguration)
        {
            routerConfiguration.ConnectionEstablishWaitTime = (routerConfiguration.ConnectionEstablishWaitTime <= TimeSpan.Zero)
                                                                  ? TimeSpan.FromMilliseconds(200)
                                                                  : routerConfiguration.ConnectionEstablishWaitTime;
            return routerConfiguration;
        }
    }
}