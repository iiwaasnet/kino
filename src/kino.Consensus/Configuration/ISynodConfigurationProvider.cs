using System;
using System.Collections.Generic;
using kino.Core;

namespace kino.Consensus.Configuration
{
    public interface ISynodConfigurationProvider
    {
        bool BelongsToSynod(string node);

        Node LocalNode { get; }

        IEnumerable<DynamicUri> Synod { get; }

        TimeSpan HeartBeatInterval { get; }

        int MissingHeartBeatsBeforeReconnect { get; }

        string IntercomEndpoint { get; }
    }
}