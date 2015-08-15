using System.Collections.Generic;

namespace rawf.Consensus
{
    public interface ISynod
    {
        string Id { get; }
        IEnumerable<INode> Members { get; }
    }
}