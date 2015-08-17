using System.Collections.Generic;

namespace rawf.Rendezvous.Consensus
{
    public class Synod : ISynod
    {
        public string Id { get; set; }
        public IEnumerable<INode> Members { get; set; }
    }
}