using kino.Consensus;
using kino.Consensus.Configuration;
using kino.Core.Diagnostics;
using kino.Core.Messaging;
using kino.Core.Sockets;
using kino.Rendezvous.Configuration;
using SynodConfiguration = kino.Consensus.Configuration.SynodConfiguration;

namespace kino.Rendezvous
{
    public class RendezvousServiceBuilder
    {

        public IRendezvousService Build(LeaseConfiguration leaseConfiguration,
                                        SocketConfiguration socketConfiguration,
                                        RendezvousConfiguration rendezvousConfiguration,
                                        ApplicationConfiguration applicationConfiguration,
                                        ILogger logger)
        {
            var socketFactory = new SocketFactory(socketConfiguration);
            var ballotGenerator = new BallotGenerator(leaseConfiguration);
            var synodConfigurationProvider = new SynodConfigurationProvider(applicationConfiguration);
            var synodeConfiguration = new SynodConfiguration(synodConfigurationProvider);
            var intercomMessageHub = new IntercomMessageHub(socketFactory, synodeConfiguration, logger);
            var roundBasedRegister = new RoundBasedRegister(intercomMessageHub,
                                                            ballotGenerator,
                                                            synodeConfiguration,
                                                            leaseConfiguration,
                                                            logger);
            var leaseProvider = new LeaseProvider(roundBasedRegister,
                                                  ballotGenerator,
                                                  leaseConfiguration,
                                                  synodeConfiguration,
                                                  logger);
            var messageSerializer = new ProtobufMessageSerializer();

            return new RendezvousService(leaseProvider,
                                         synodeConfiguration,
                                         socketFactory,
                                         messageSerializer,
                                         rendezvousConfiguration,
                                         logger);
        }
    }
}