using kino.Connectivity;
using kino.Consensus;
using kino.Consensus.Configuration;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Messaging;
using kino.Rendezvous.Configuration;
#if !NET47
using NetMQ;

#endif

namespace kino.Rendezvous
{
    public partial class Rendezvous
    {
        private IRendezvousService Build()
        {
#if !NET47
            BufferPool.SetCustomBufferPool(new CustomBufferPool());
#endif

            var logger = resolver.Resolve<ILogger>();
            var applicationConfig = resolver.Resolve<RendezvousServiceConfiguration>();
            var messageWireFormatter =
#if NET47
                resolver.Resolve<IMessageWireFormatter>() ?? new MessageWireFormatterV5();
#else
                resolver.Resolve<IMessageWireFormatter>() ?? new MessageWireFormatterV6_1();
#endif
            var socketFactory = new SocketFactory(messageWireFormatter, applicationConfig.Socket);
            var synodConfigProvider = new SynodConfigurationProvider(applicationConfig.Synod);

            var instanceNameResolver = resolver.Resolve<IInstanceNameResolver>() ?? new InstanceNameResolver();

            var performanceCounterManager = new PerformanceCounterManager<KinoPerformanceCounters>(instanceNameResolver, logger);
            var intercomMessageHub = new IntercomMessageHub(socketFactory,
                                                            synodConfigProvider,
                                                            performanceCounterManager,
                                                            logger);
            var ballotGenerator = new BallotGenerator(applicationConfig.Lease);
            var roundBasedRegister = new RoundBasedRegister(intercomMessageHub,
                                                            ballotGenerator,
                                                            synodConfigProvider,
                                                            applicationConfig.Lease,
                                                            logger);
            var leaseProvider = new LeaseProvider(roundBasedRegister,
                                                  ballotGenerator,
                                                  applicationConfig.Lease,
                                                  synodConfigProvider,
                                                  logger);

            var serializer = new ProtobufMessageSerializer();
            var configProvider = new RendezvousConfigurationProvider(applicationConfig.Rendezvous);
            var localSocketFactory = new LocalSocketFactory();
            var partnerNetworksConfigProvider = new PartnerNetworksConfigurationProvider(applicationConfig.Partners);

            var partnerNetworkConnectorManager = new PartnerNetworkConnectorManager(socketFactory,
                                                                                    localSocketFactory,
                                                                                    performanceCounterManager,
                                                                                    logger,
                                                                                    partnerNetworksConfigProvider);
            var service = new RendezvousService(leaseProvider,
                                                synodConfigProvider,
                                                socketFactory,
                                                serializer,
                                                localSocketFactory,
                                                configProvider,
                                                partnerNetworkConnectorManager,
                                                performanceCounterManager,
                                                logger);

            return service;
        }
    }
}