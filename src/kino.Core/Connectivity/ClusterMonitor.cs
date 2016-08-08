using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;

namespace kino.Core.Connectivity
{
    public class ClusterMonitor : IClusterMonitor
    {
        private CancellationTokenSource messageProcessingToken;
        private readonly IClusterMembership clusterMembership;
        private readonly RouterConfiguration routerConfiguration;
        private Task sendingMessages;
        private Task listenningMessages;
        private readonly IClusterMessageSender clusterMessageSender;
        private readonly IClusterMessageListener clusterMessageListener;
        private readonly IRouteDiscovery routeDiscovery;
        private readonly ISecurityProvider securityProvider;

        public ClusterMonitor(RouterConfiguration routerConfiguration,
                              IClusterMembership clusterMembership,
                              IClusterMessageSender clusterMessageSender,
                              IClusterMessageListener clusterMessageListener,
                              IRouteDiscovery routeDiscovery,
                              ISecurityProvider securityProvider)
        {
            this.clusterMessageSender = clusterMessageSender;
            this.clusterMessageListener = clusterMessageListener;
            this.routeDiscovery = routeDiscovery;
            this.securityProvider = securityProvider;
            this.routerConfiguration = routerConfiguration;
            this.clusterMembership = clusterMembership;
        }

        public bool Start(TimeSpan startTimeout)
            => StartProcessingClusterMessages(startTimeout);

        public void Stop()
            => StopProcessingClusterMessages();

        private bool StartProcessingClusterMessages(TimeSpan startTimeout)
        {
            messageProcessingToken = new CancellationTokenSource();
            const int participantCount = 3;
            using (var gateway = new Barrier(participantCount))
            {
                sendingMessages = Task.Factory.StartNew(_ => clusterMessageSender.StartBlockingSendMessages(messageProcessingToken.Token, gateway),
                                                        TaskCreationOptions.LongRunning);
                listenningMessages = Task.Factory.StartNew(_ => clusterMessageListener.StartBlockingListenMessages(RestartProcessingClusterMessages, messageProcessingToken.Token, gateway),
                                                           TaskCreationOptions.LongRunning);
                var started = gateway.SignalAndWait(startTimeout, messageProcessingToken.Token);
                if (started)
                {
                    routeDiscovery.Start();
                }

                return started;
            }
        }

        private void StopProcessingClusterMessages()
        {
            routeDiscovery.Stop();
            messageProcessingToken?.Cancel();
            sendingMessages?.Wait();
            listenningMessages?.Wait();
            messageProcessingToken?.Dispose();
        }

        private void RestartProcessingClusterMessages()
        {
            StopProcessingClusterMessages();
            StartProcessingClusterMessages(TimeSpan.FromMilliseconds(-1));
        }

        public void RegisterSelf(IEnumerable<MessageIdentifier> messageHandlers, string domain)
        {
            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                             SocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                             MessageContracts = messageHandlers.Select(mi => new MessageContract
                                                                                             {
                                                                                                 Version = mi.Version,
                                                                                                 Identity = mi.Identity,
                                                                                                 Partition = mi.Partition
                                                                                             }).ToArray()
                                         },
                                         domain);
            message.As<Message>().SignMessage(securityProvider);
            clusterMessageSender.EnqueueMessage(message);
        }

        public void UnregisterSelf(IEnumerable<MessageIdentifier> messageIdentifiers)
        {
            var messageGroups = messageIdentifiers.Select(mi => new
                                                                {
                                                                    Message = new MessageContract
                                                                              {
                                                                                  Identity = mi.Identity,
                                                                                  Version = mi.Version,
                                                                                  Partition = mi.Partition
                                                                              },
                                                                    Domain = securityProvider.GetDomain(mi.Identity)
                                                                })
                                                  .GroupBy(mh => mh.Domain);

            foreach (var group in messageGroups)
            {
                var message = Message.Create(new UnregisterMessageRouteMessage
                                             {
                                                 Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                                 SocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                                 MessageContracts = group.Select(g => g.Message).ToArray()
                                             },
                                             group.Key);
                message.As<Message>().SignMessage(securityProvider);

                clusterMessageSender.EnqueueMessage(message);
            }
        }

        public void RequestClusterRoutes()
        {
            foreach (var domain in securityProvider.GetAllowedDomains())
            {
                var message = Message.Create(new RequestClusterMessageRoutesMessage
                                             {
                                                 RequestorSocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                                 RequestorUri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress()
                                             },
                                             domain);
                message.As<Message>().SignMessage(securityProvider);

                clusterMessageSender.EnqueueMessage(message);
            }
        }

        public IEnumerable<SocketEndpoint> GetClusterMembers()
            => clusterMembership.GetClusterMembers();

        public void DiscoverMessageRoute(MessageIdentifier messageIdentifier)
            => routeDiscovery.RequestRouteDiscovery(messageIdentifier);
    }
}