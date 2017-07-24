using System;
using System.Collections.Generic;

namespace kino.Consensus.Configuration
{
    public interface ISynodConfigurationProvider
    {
        Uri LocalNode { get; }

        IEnumerable<Uri> Synod { get; }

        TimeSpan HeartBeatInterval { get; }

        int MissingHeartBeatsBeforeReconnect { get; }

        Uri IntercomEndpoint { get; }
    }
}