using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using C5;
using kino.Cluster.Configuration;
using kino.Core;
using kino.Core.Diagnostics;
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
        private Task listeningMessages;
        private readonly IScaleOutConfigurationProvider scaleOutConfigurationProvider;
        private readonly IAutoDiscoverySender autoDiscoverySender;
        private readonly IAutoDiscoveryListener autoDiscoveryListener;
        private readonly IHeartBeatSenderConfigurationProvider heartBeatConfigurationProvider;
        private readonly IRouteDiscovery routeDiscovery;
        private readonly ISecurityProvider securityProvider;
        private readonly RouteDiscoveryConfiguration routeDiscoveryConfig;
        private readonly ILogger logger;
        private readonly Timer clusterRoutesRequestTimer;
        private readonly C5Random randomizer;

        public ClusterMonitor(IScaleOutConfigurationProvider scaleOutConfigurationProvider,
                              IAutoDiscoverySender autoDiscoverySender,
                              IAutoDiscoveryListener autoDiscoveryListener,
                              IHeartBeatSenderConfigurationProvider heartBeatConfigurationProvider,
                              IRouteDiscovery routeDiscovery,
                              ISecurityProvider securityProvider,
                              ClusterMembershipConfiguration clusterMembershipConfiguration,
                              ILogger logger)
        {
            this.scaleOutConfigurationProvider = scaleOutConfigurationProvider;
            this.autoDiscoverySender = autoDiscoverySender;
            this.autoDiscoveryListener = autoDiscoveryListener;
            this.heartBeatConfigurationProvider = heartBeatConfigurationProvider;
            this.routeDiscovery = routeDiscovery;
            this.securityProvider = securityProvider;
            routeDiscoveryConfig = clusterMembershipConfiguration.RouteDiscovery;
            this.logger = logger;
            randomizer = new C5Random();
            clusterRoutesRequestTimer = new Timer(_ => RequestClusterRoutes(), null, TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
        }

        public void Start()
        {
            StartProcessingClusterMessages();
        }

        public void Stop()
        {
            UnregisterSelf();
            StopProcessingClusterMessages();
        }

        private void StartProcessingClusterMessages()
        {
            messageProcessingToken = new CancellationTokenSource();
            const int participantCount = 3;
            using (var gateway = new Barrier(participantCount))
            {
                // 1. Start listening for messages
                listeningMessages = Task.Factory.StartNew(_ => autoDiscoveryListener.StartBlockingListenMessages(RestartProcessingClusterMessages, messageProcessingToken.Token, gateway),
                                                          TaskCreationOptions.LongRunning);
                // 2. Start sending
                sendingMessages = Task.Factory.StartNew(_ => autoDiscoverySender.StartBlockingSendMessages(messageProcessingToken.Token, gateway),
                                                        TaskCreationOptions.LongRunning);
                gateway.SignalAndWait(messageProcessingToken.Token);

                routeDiscovery.Start();
            }

            clusterRoutesRequestTimer.Change(RandomizeClusterAutoDiscoveryStartDelay(), routeDiscoveryConfig.ClusterAutoDiscoveryPeriod);
        }

        private void StopProcessingClusterMessages()
        {
            clusterRoutesRequestTimer.Change(TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
            routeDiscovery.Stop();
            messageProcessingToken?.Cancel();
            sendingMessages?.Wait();
            listeningMessages?.Wait();
            messageProcessingToken?.Dispose();
        }

        private void RestartProcessingClusterMessages()
        {
            StopProcessingClusterMessages();
            StartProcessingClusterMessages();
        }

        private void RequestClusterRoutes()
        {
            try
            {
                var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();
                foreach (var domain in securityProvider.GetAllowedDomains())
                {
                    var message = Message.Create(new RequestClusterMessageRoutesMessage
                                                 {
                                                     RequestorNodeIdentity = scaleOutAddress.Identity,
                                                     RequestorUri = scaleOutAddress.Uri
                                                 })
                                         .As<Message>();
                    message.SetDomain(domain);
                    message.As<Message>().SignMessage(securityProvider);

                    autoDiscoverySender.EnqueueMessage(message);

                    logger.Info($"Request to discover cluster routes for Domain [{domain}] sent.");
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private void UnregisterSelf()
        {
            var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();
            foreach (var domain in securityProvider.GetAllowedDomains())
            {
                var message = Message.Create(new UnregisterNodeMessage
                                             {
                                                 Uri = scaleOutAddress.Uri,
                                                 ReceiverNodeIdentity = scaleOutAddress.Identity,
                                             })
                                     .As<Message>();
                message.SetDomain(domain);
                message.SignMessage(securityProvider);

                autoDiscoverySender.EnqueueMessage(message);

                logger.Debug($"Unregistering self {scaleOutAddress.Identity.GetAnyString()} from Domain {domain}");
            }

            routeDiscoveryConfig.UnregisterMessageSendTimeout.Sleep();
        }

        public void RegisterSelf(IEnumerable<MessageRoute> registrations, string domain)
        {
            var receiverRoutes = registrations.GroupBy(r => r.Receiver)
                                              .Select(g => new
                                                           {
                                                               ReceiverIdentifier = g.Key.Identity,
                                                               Messages = g.Key.IsActor()
                                                                              ? g.Select(x => x.Message)
                                                                              : Enumerable.Empty<MessageIdentifier>()
                                                           })
                                              .Select(r => new RouteRegistration
                                                           {
                                                               ReceiverIdentity = r.ReceiverIdentifier,
                                                               MessageContracts = r.Messages.Select(m => new MessageContract
                                                                                                         {
                                                                                                             Identity = m.Identity,
                                                                                                             Partition = m.Partition,
                                                                                                             Version = m.Version
                                                                                                         })
                                                                                   .ToArray()
                                                           }).ToArray();
            var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();

            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = scaleOutAddress.Uri,
                                             NodeIdentity = scaleOutAddress.Identity,
                                             Health = new Messaging.Messages.Health
                                                      {
                                                          Uri = heartBeatConfigurationProvider.GetHeartBeatAddress(),
                                                          HeartBeatInterval = heartBeatConfigurationProvider.GetHeartBeatInterval()
                                                      },
                                             Routes = receiverRoutes
                                         })
                                 .As<Message>();
            message.SetDomain(domain);
            message.SignMessage(securityProvider);
            autoDiscoverySender.EnqueueMessage(message);
        }

        public void UnregisterSelf(IEnumerable<MessageRoute> messageRoutes)
        {
            //TODO: MessageRoute.Receiver can be NULL. Check how grouping will work in this case
            var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();
            var routeByDomain = GetMessageHubs(messageRoutes).Concat(GetActors(messageRoutes))
                                                             .GroupBy(mr => mr.Domain)
                                                             .Select(g => new
                                                                          {
                                                                              Domain = g.Key,
                                                                              MessageRoutes = g.GroupBy(mr => mr.MessageRoute.Receiver)
                                                                                               .Select(rg => new
                                                                                                             {
                                                                                                                 Receiver = rg.Key,
                                                                                                                 MessageContracts = rg.Where(x => x.MessageRoute.Message != null)
                                                                                                                                      .Select(x => x.MessageRoute.Message)
                                                                                                             })
                                                                          });

            foreach (var domainRoutes in routeByDomain)
            {
                var message = Message.Create(new UnregisterMessageRouteMessage
                                             {
                                                 Uri = scaleOutAddress.Uri,
                                                 ReceiverNodeIdentity = scaleOutAddress.Identity,
                                                 Routes = domainRoutes.MessageRoutes
                                                                      .Select(r => new RouteRegistration
                                                                                   {
                                                                                       ReceiverIdentity = r.Receiver?.Identity,
                                                                                       MessageContracts = r.MessageContracts
                                                                                                           .Select(mc => new MessageContract
                                                                                                                         {
                                                                                                                             Identity = mc.Identity,
                                                                                                                             Version = mc.Version,
                                                                                                                             Partition = mc.Partition
                                                                                                                         })
                                                                                                           .ToArray()
                                                                                   })
                                                                      .ToArray()
                                             })
                                     .As<Message>();
                message.SetDomain(domainRoutes.Domain);
                message.SignMessage(securityProvider);

                autoDiscoverySender.EnqueueMessage(message);
            }
        }

        private IEnumerable<MessageRouteDomainMap> GetActors(IEnumerable<MessageRoute> messageRoutes)
            => messageRoutes.Where(mr => !mr.Receiver.IsSet() || mr.Receiver.IsActor())
                            .Select(mr => new MessageRouteDomainMap
                                          {
                                              MessageRoute = mr,
                                              Domain = securityProvider.GetDomain(mr.Message.Identity)
                                          });

        private IEnumerable<MessageRouteDomainMap> GetMessageHubs(IEnumerable<MessageRoute> messageRoutes)
            => messageRoutes.Where(mr => mr.Receiver.IsMessageHub())
                            .SelectMany(mr => securityProvider.GetAllowedDomains()
                                                              .Select(dom =>
                                                                          new MessageRouteDomainMap
                                                                          {
                                                                              MessageRoute = mr,
                                                                              Domain = dom
                                                                          }));

        public void DiscoverMessageRoute(MessageRoute messageRoute)
            => routeDiscovery.RequestRouteDiscovery(messageRoute);

        private TimeSpan RandomizeClusterAutoDiscoveryStartDelay()
            => routeDiscoveryConfig.ClusterAutoDiscoveryStartDelay
                                   .MultiplyBy(randomizer.Next(1, routeDiscoveryConfig.ClusterAutoDiscoveryStartDelayMaxMultiplier));
    }
}