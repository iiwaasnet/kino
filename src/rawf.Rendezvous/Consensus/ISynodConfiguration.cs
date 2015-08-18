using System;
using System.Collections.Generic;

namespace rawf.Rendezvous.Consensus
{
    public interface ISynodConfiguration
    {
        Node LocalNode { get; }
        IEnumerable<Uri> Synod { get; }

        bool BelongsToSynod(Uri node);
    }
}