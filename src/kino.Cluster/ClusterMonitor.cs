using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly ILogger logger;
        private DateTime startTime;
        //TODO: Move to config
        private readonly TimeSpan unregisterMessageSendTimeout = TimeSpan.FromMilliseconds(500);
        //TODO: Move to config
        private readonly TimeSpan requestClusterRoutesOnStartWindow = TimeSpan.FromSeconds(30);

        public ClusterMonitor(IScaleOutConfigurationProvider scaleOutConfigurationProvider,
                              IAutoDiscoverySender autoDiscoverySender,
                              IAutoDiscoveryListener autoDiscoveryListener,
                              IHeartBeatSenderConfigurationProvider heartBeatConfigurationProvider,
                              IRouteDiscovery routeDiscovery,
                              ISecurityProvider securityProvider,
                              ILogger logger)
        {
            this.scaleOutConfigurationProvider = scaleOutConfigurationProvider;
            this.autoDiscoverySender = autoDiscoverySender;
            this.autoDiscoveryListener = autoDiscoveryListener;
            this.heartBeatConfigurationProvider = heartBeatConfigurationProvider;
            this.routeDiscovery = routeDiscovery;
            this.securityProvider = securityProvider;
            this.logger = logger;
        }

        public void Start()
        {
            startTime = DateTime.UtcNow;
            StartProcessingClusterMessages();
            RequestClusterRoutes();
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
        }

        private void StopProcessingClusterMessages()
        {
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
            RequestClusterRoutes();
        }

        private void RequestClusterRoutes()
        {
            if (startTime - DateTime.UtcNow < requestClusterRoutesOnStartWindow)
            {
                try
                {
                    var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();
                    foreach (var domain in securityProvider.GetAllowedDomains())
                    {
                        var message = Message.Create(new RequestClusterMessageRoutesMessage
                                                     {
                                                         RequestorNodeIdentity = scaleOutAddress.Identity,
                                                         RequestorUri = scaleOutAddress.Uri.ToSocketAddress()
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
        }

        private void UnregisterSelf()
        {
            var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();
            foreach (var domain in securityProvider.GetAllowedDomains())
            {
                var message = Message.Create(new UnregisterNodeMessage
                                             {
                                                 Uri = scaleOutAddress.Uri.ToSocketAddress(),
                                                 ReceiverNodeIdentity = scaleOutAddress.Identity,
                                             })
                                     .As<Message>();
                message.SetDomain(domain);
                message.SignMessage(securityProvider);

                autoDiscoverySender.EnqueueMessage(message);

                logger.Debug($"Unregistering self {scaleOutAddress.Identity.GetAnyString()} from Domain {domain}");
            }

            unregisterMessageSendTimeout.Sleep();
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
                                             Uri = scaleOutAddress.Uri.ToSocketAddress(),
                                             NodeIdentity = scaleOutAddress.Identity,
                                             Health = new Messaging.Messages.Health
                                                      {
                                                          Uri = heartBeatConfigurationProvider.GetHeartBeatAddress()
                                                                                              .ToSocketAddress(),
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
                                                 Uri = scaleOutAddress.Uri.ToSocketAddress(),
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
    }
}