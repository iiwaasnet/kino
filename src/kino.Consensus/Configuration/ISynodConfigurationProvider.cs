using System;
using System.Collections.Generic;
using kino.Core;

namespace kino.Consensus.Configuration
{
    public interface ISynodConfigurationProvider
    {
        bool BelongsToSynod(Uri node);

        Node LocalNode { get; }

        IEnumerable<Location> Synod { get; }

        TimeSpan HeartBeatInterval { get; }

        int MissingHeartBeatsBeforeReconnect { get; }

        Uri IntercomEndpoint { get; }
    }
}