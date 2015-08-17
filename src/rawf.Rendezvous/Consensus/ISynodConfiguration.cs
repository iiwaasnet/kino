using System;
using System.Collections.Generic;

namespace rawf.Rendezvous.Consensus
{
    public interface ISynodConfiguration
    {
        INode LocalNode { get; }
        IEnumerable<Uri> Synod { get; }

        bool BelongsToSynod(Uri node);
    }
}