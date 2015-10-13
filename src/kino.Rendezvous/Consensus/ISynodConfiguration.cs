using System;
using System.Collections.Generic;
using kino.Connectivity;

namespace kino.Rendezvous.Consensus
{
    public interface ISynodConfiguration
    {
        Node LocalNode { get; }
        IEnumerable<Uri> Synod { get; }

        bool BelongsToSynod(Uri node);
    }
}