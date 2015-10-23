using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Diagnostics;
using kino.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Sockets;

namespace kino.Connectivity
{
    internal class ClusterMonitor : IClusterMonitor
    {
        private CancellationTokenSource messageProcessingToken;
        private readonly IClusterMembership clusterMembership;
        private readonly RouterConfiguration routerConfiguration;
        private Task sendingMessages;
        private Task listenningMessages;
        private readonly IClusterMessageSender clusterMessageSender;
        private readonly IClusterMessageListener clusterMessageListener;

        public ClusterMonitor(RouterConfiguration routerConfiguration,
                              IClusterMembership clusterMembership,
                              IClusterMessageSender clusterMessageSender,
                              IClusterMessageListener clusterMessageListener)
        {
            this.clusterMessageSender = clusterMessageSender;
            this.clusterMessageListener = clusterMessageListener;
            this.routerConfiguration = routerConfiguration;
            this.clusterMembership = clusterMembership;
        }

        public void Start()
        {
            StartProcessingClusterMessages();
        }

        public void Stop()
        {
            StopProcessingClusterMessages();
        }

        private void StartProcessingClusterMessages()
        {
            messageProcessingToken = new CancellationTokenSource();
            const int participantCount = 3;
            using (var gateway = new Barrier(participantCount))
            {
                sendingMessages = Task.Factory.StartNew(_ => clusterMessageSender.StartBlockingSendMessages(messageProcessingToken.Token, gateway),
                                                        TaskCreationOptions.LongRunning);
                listenningMessages = Task.Factory.StartNew(_ => clusterMessageListener.StartBlockingListenMessages(RestartProcessingClusterMessages, messageProcessingToken.Token, gateway),
                                                           TaskCreationOptions.LongRunning);
                gateway.SignalAndWait(messageProcessingToken.Token);
            }
        }

        private void StopProcessingClusterMessages()
        {
            messageProcessingToken.Cancel();
            sendingMessages.Wait();
            listenningMessages.Wait();
            messageProcessingToken.Dispose();
        }

        private void RestartProcessingClusterMessages()
        {
            StopProcessingClusterMessages();
            StartProcessingClusterMessages();
        }

        public void RegisterSelf(IEnumerable<MessageIdentifier> messageHandlers)
        {
            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                             SocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                             MessageContracts = messageHandlers.Select(mi => new MessageContract
                                                                                             {
                                                                                                 Version = mi.Version,
                                                                                                 Identity = mi.Identity
                                                                                             }).ToArray()
                                         });
            clusterMessageSender.EnqueueMessage(message);
        }

        public void UnregisterSelf(IEnumerable<MessageIdentifier> messageIdentifiers)
        {
            var message = Message.Create(new UnregisterMessageRouteMessage
                                         {
                                             Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                             SocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                             MessageContracts = messageIdentifiers
                                                 .Select(mi => new MessageContract
                                                               {
                                                                   Identity = mi.Identity,
                                                                   Version = mi.Version
                                                               }
                                                 )
                                                 .ToArray()
                                         });
            clusterMessageSender.EnqueueMessage(message);
        }

        public void RequestClusterRoutes()
        {
            var message = Message.Create(new RequestClusterMessageRoutesMessage
                                         {
                                             RequestorSocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                             RequestorUri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress()
                                         });
            clusterMessageSender.EnqueueMessage(message);
        }

        public IEnumerable<SocketEndpoint> GetClusterMembers()
            => clusterMembership.GetClusterMembers();

        public void DiscoverMessageRoute(MessageIdentifier messageIdentifier)
        {
            var message = Message.Create(new DiscoverMessageRouteMessage
                                         {
                                             RequestorSocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                             RequestorUri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                             MessageContract = new MessageContract
                                                               {
                                                                   Version = messageIdentifier.Version,
                                                                   Identity = messageIdentifier.Identity
                                                               }
                                         });
            clusterMessageSender.EnqueueMessage(message);
        }
    }
}