using System;
using System.Collections.Generic;
using kino.Core;

namespace kino.Consensus.Configuration
{
    public interface ISynodConfiguration
    {
        bool BelongsToSynod(Uri node);

        Node LocalNode { get; }

        IEnumerable<Uri> Synod { get; }

        TimeSpan HeartBeatInterval { get; }

        int MissingHeartBeatsBeforeReconnect { get; }

        Uri IntercomEndpoint { get; }
    }
}