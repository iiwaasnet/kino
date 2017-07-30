using System;
using System.Collections.Generic;
using kino.Core;

namespace kino.Consensus.Configuration
{
    public interface ISynodConfigurationProvider
    {
        bool BelongsToSynod(Uri node);

        void ForceNodeIpAddressResolution(Uri nodeUri);

        Node LocalNode { get; }

        IEnumerable<Uri> Synod { get; }

        TimeSpan HeartBeatInterval { get; }

        int MissingHeartBeatsBeforeReconnect { get; }

        Uri IntercomEndpoint { get; }
    }
}