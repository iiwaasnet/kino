using kino.Consensus.Configuration;

namespace kino.Rendezvous.Configuration
{
    public class RendezvousServiceConfiguration
    {
        public RendezvousConfiguration Rendezvous { get; set; }

        public SynodConfiguration Synod { get; set; }

        public LeaseConfiguration Lease { get; set; }
    }
}