using kino.Consensus;
using kino.Core.Diagnostics;
using kino.Core.Messaging;
using kino.Core.Sockets;
using kino.Rendezvous.Configuration;
using SynodConfiguration = kino.Consensus.Configuration.SynodConfiguration;

namespace kino.Rendezvous
{
    public class Composer
    {
        public IRendezvousService BuildRendezvousService(ApplicationConfiguration applicationConfiguration,
                                                         ILogger logger)
        {
            return BuildRendezvousService(null, applicationConfiguration, logger);
        }

        public IRendezvousService BuildRendezvousService(SocketConfiguration socketConfiguration,
                                                         ApplicationConfiguration applicationConfiguration,
                                                         ILogger logger)
        {
            var socketFactory = new SocketFactory(socketConfiguration);
            var ballotGenerator = new BallotGenerator(applicationConfiguration.Lease);
            var synodConfigurationProvider = new SynodConfigurationProvider(applicationConfiguration.Synod);
            var synodeConfiguration = new SynodConfiguration(synodConfigurationProvider);
            var intercomMessageHub = new IntercomMessageHub(socketFactory, synodeConfiguration, logger);
            var roundBasedRegister = new RoundBasedRegister(intercomMessageHub,
                                                            ballotGenerator,
                                                            synodeConfiguration,
                                                            applicationConfiguration.Lease,
                                                            logger);
            var leaseProvider = new LeaseProvider(roundBasedRegister,
                                                  ballotGenerator,
                                                  applicationConfiguration.Lease,
                                                  synodeConfiguration,
                                                  logger);
            var messageSerializer = new ProtobufMessageSerializer();

            return new RendezvousService(leaseProvider,
                                         synodeConfiguration,
                                         socketFactory,
                                         messageSerializer,
                                         applicationConfiguration.Rendezvous,
                                         logger);
        }
    }
}