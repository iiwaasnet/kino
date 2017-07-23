using System;
using System.Collections.Generic;
using kino.Core;

namespace kino.Consensus.Configuration
{
    public interface ISynodConfiguration
    {
        Node LocalNode { get; }

        IEnumerable<Uri> Synod { get; }

        TimeSpan HeartBeatInterval { get; set; }

        int MissingHeartBeatsBeforeReconnect { get; set; }

        bool BelongsToSynod(Uri node);
    }
}