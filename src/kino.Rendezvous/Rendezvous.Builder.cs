using kino.Connectivity;
using kino.Consensus;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Messaging;
using kino.Rendezvous.Configuration;
using SynodConfiguration = kino.Consensus.Configuration.SynodConfiguration;

namespace kino.Rendezvous
{
    public partial class Rendezvous
    {
        private IRendezvousService Build()
        {
            var logger = resolver.Resolve<ILogger>();
            var applicationConfig = resolver.Resolve<ApplicationConfiguration>();
            var socketFactory = new SocketFactory(resolver.Resolve<SocketConfiguration>());
            var synodConfigProvider = new SynodConfigurationProvider(applicationConfig.Synod);
            var synodConfig = new SynodConfiguration(synodConfigProvider);
            var instanceNameResolver = resolver.Resolve<IInstanceNameResolver>() ?? new InstanceNameResolver();
            var performanceCounterManager = new PerformanceCounterManager<KinoPerformanceCounters>(instanceNameResolver,
                                                                                                   logger);
            var intercomMessageHub = new IntercomMessageHub(socketFactory,
                                                            synodConfig,
                                                            performanceCounterManager,
                                                            logger);
            var ballotGenerator = new BallotGenerator(applicationConfig.Lease);
            var roundBasedRegister = new RoundBasedRegister(intercomMessageHub,
                                                            ballotGenerator,
                                                            synodConfig,
                                                            applicationConfig.Lease,
                                                            logger);
            var leaseProvider = new LeaseProvider(roundBasedRegister,
                                                  ballotGenerator,
                                                  applicationConfig.Lease,
                                                  synodConfig,
                                                  logger);

            var serializer = new ProtobufMessageSerializer();
            var service = new RendezvousService(leaseProvider,
                                                synodConfig,
                                                socketFactory,
                                                serializer,
                                                applicationConfig.Rendezvous,
                                                performanceCounterManager,
                                                logger);

            return service;
        }
    }
}