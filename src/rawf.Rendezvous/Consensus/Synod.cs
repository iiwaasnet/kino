using System.Collections.Generic;

namespace rawf.Rendezvous.Consensus
{
    public class Synod
    {
        public string Id { get; set; }
        public IEnumerable<Node> Members { get; set; }
    }
}