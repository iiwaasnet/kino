using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Configuration;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Cluster
{
    public class ClusterMonitor : IClusterMonitor
    {
        private CancellationTokenSource messageProcessingToken;
        private Task sendingMessages;
        private Task listenningMessages;
        private readonly IScaleOutConfigurationProvider scaleOutConfigurationProvider;
        private readonly IAutoDiscoverySender autoDiscoverySender;
        private readonly IAutoDiscoveryListener autoDiscoveryListener;
        private readonly IHeartBeatSenderConfigurationProvider heartBeatConfigurationProvider;
        private readonly IRouteDiscovery routeDiscovery;
        private readonly ISecurityProvider securityProvider;

        public ClusterMonitor(IScaleOutConfigurationProvider scaleOutConfigurationProvider,
                              IAutoDiscoverySender autoDiscoverySender,
                              IAutoDiscoveryListener autoDiscoveryListener,
                              IHeartBeatSenderConfigurationProvider heartBeatConfigurationProvider,
                              IRouteDiscovery routeDiscovery,
                              ISecurityProvider securityProvider)
        {
            this.scaleOutConfigurationProvider = scaleOutConfigurationProvider;
            this.autoDiscoverySender = autoDiscoverySender;
            this.autoDiscoveryListener = autoDiscoveryListener;
            this.heartBeatConfigurationProvider = heartBeatConfigurationProvider;
            this.routeDiscovery = routeDiscovery;
            this.securityProvider = securityProvider;
        }

        public void Start()
            => StartProcessingClusterMessages();

        public void Stop()
            => StopProcessingClusterMessages();

        private void StartProcessingClusterMessages()
        {
            messageProcessingToken = new CancellationTokenSource();
            const int participantCount = 3;
            using (var gateway = new Barrier(participantCount))
            {
                sendingMessages = Task.Factory.StartNew(_ => autoDiscoverySender.StartBlockingSendMessages(messageProcessingToken.Token, gateway),
                                                        TaskCreationOptions.LongRunning);
                listenningMessages = Task.Factory.StartNew(_ => autoDiscoveryListener.StartBlockingListenMessages(RestartProcessingClusterMessages, messageProcessingToken.Token, gateway),
                                                           TaskCreationOptions.LongRunning);
                gateway.SignalAndWait(messageProcessingToken.Token);

                routeDiscovery.Start();
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
            StartProcessingClusterMessages();
        }

        public void RegisterSelf(IEnumerable<Identifier> messageHandlers, string domain)
        {
            var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();

            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = scaleOutAddress.Uri.ToSocketAddress(),
                                             SocketIdentity = scaleOutAddress.Identity,
                                             Health = new Messaging.Messages.Health
                                                      {
                                                          Uri = heartBeatConfigurationProvider.GetHeartBeatAddress()
                                                                                              .ToSocketAddress(),
                                                          HeartBeatInterval = heartBeatConfigurationProvider.GetHeartBeatInterval()
                                                      },
                                             MessageContracts = messageHandlers.Select(mi => new MessageContract
                                                                                             {
                                                                                                 Version = mi.Version,
                                                                                                 Identity = mi.Identity,
                                                                                                 Partition = mi.Partition,
                                                                                                 IsAnyIdentifier = mi is AnyIdentifier
                                                                                             }).ToArray()
                                         },
                                         domain);
            message.As<Message>().SignMessage(securityProvider);
            autoDiscoverySender.EnqueueMessage(message);
        }

        public void UnregisterSelf(IEnumerable<Identifier> messageIdentifiers)
        {
            var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();
            var messageGroups = GetMessageHubs(messageIdentifiers).Concat(GetMessageHandlers(messageIdentifiers))
                                                                  .GroupBy(mh => mh.Domain);

            foreach (var group in messageGroups)
            {
                var message = Message.Create(new UnregisterMessageRouteMessage
                                             {
                                                 Uri = scaleOutAddress.Uri.ToSocketAddress(),
                                                 SocketIdentity = scaleOutAddress.Identity,
                                                 MessageContracts = group.Select(g => g.Message).ToArray()
                                             },
                                             group.Key);
                message.As<Message>().SignMessage(securityProvider);

                autoDiscoverySender.EnqueueMessage(message);
            }
        }

        private IEnumerable<MessageDomainMap> GetMessageHandlers(IEnumerable<Identifier> messageIdentifiers)
            => messageIdentifiers.Where(mi => !mi.IsMessageHub())
                                 .Select(mi => new MessageDomainMap
                                               {
                                                   Message = new MessageContract
                                                             {
                                                                 Identity = mi.Identity,
                                                                 Version = mi.Version,
                                                                 Partition = mi.Partition,
                                                                 IsAnyIdentifier = false
                                                             },
                                                   Domain = securityProvider.GetDomain(mi.Identity)
                                               });

        private IEnumerable<MessageDomainMap> GetMessageHubs(IEnumerable<Identifier> messageIdentifiers)
            => messageIdentifiers.Where(mi => mi.IsMessageHub())
                                 .SelectMany(mi => securityProvider.GetAllowedDomains().Select(dom =>
                                                                                                   new MessageDomainMap
                                                                                                   {
                                                                                                       Message = new MessageContract
                                                                                                                 {
                                                                                                                     Identity = mi.Identity,
                                                                                                                     Version = mi.Version,
                                                                                                                     Partition = mi.Partition,
                                                                                                                     IsAnyIdentifier = true
                                                                                                                 },
                                                                                                       Domain = dom
                                                                                                   }));

        public void DiscoverMessageRoute(Identifier messageIdentifier)
            => routeDiscovery.RequestRouteDiscovery(messageIdentifier);
    }

    internal class MessageDomainMap
    {
        internal MessageContract Message { get; set; }

        internal string Domain { get; set; }
    }
}