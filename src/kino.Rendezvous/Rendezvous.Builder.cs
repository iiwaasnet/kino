using kino.Connectivity;
using kino.Consensus;
using kino.Consensus.Configuration;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Messaging;
using kino.Rendezvous.Configuration;

namespace kino.Rendezvous
{
    public partial class Rendezvous
    {
        private IRendezvousService Build()
        {
            var logger = resolver.Resolve<ILogger>();
            var applicationConfig = resolver.Resolve<RendezvousServiceConfiguration>();
            var messageWireFormatter = resolver.Resolve<IMessageWireFormatter>() ?? new MessageWireFormatterV6();
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
            var service = new RendezvousService(leaseProvider,
                                                synodConfigProvider,
                                                socketFactory,
                                                serializer,
                                                configProvider,
                                                performanceCounterManager,
                                                logger);

            return service;
        }
    }
}