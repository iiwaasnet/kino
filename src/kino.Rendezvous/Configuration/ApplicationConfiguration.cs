using kino.Consensus.Configuration;

namespace kino.Rendezvous.Configuration
{
    public class ApplicationConfiguration
    {
        public RendezvousConfiguration Rendezvous { get; set; }

        public SynodConfiguration Synod { get; set; }

        public LeaseConfiguration Lease { get; set; }
    }
}