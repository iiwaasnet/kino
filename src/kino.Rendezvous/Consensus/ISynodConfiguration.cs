using System;
using System.Collections.Generic;

namespace kino.Rendezvous.Consensus
{
    public interface ISynodConfiguration
    {
        Node LocalNode { get; }
        IEnumerable<Uri> Synod { get; }

        bool BelongsToSynod(Uri node);
    }
}