using System.Collections.Generic;

namespace rawf.Rendezvous.Consensus
{
    public interface ISynod
    {
        string Id { get; }
        IEnumerable<INode> Members { get; }
    }
}