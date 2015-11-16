using System;
using System.Collections.Generic;
using kino.Core.Connectivity;

namespace kino.Consensus.Configuration
{
    public interface ISynodConfiguration
    {
        Node LocalNode { get; }
        IEnumerable<Uri> Synod { get; }

        bool BelongsToSynod(Uri node);
    }
}