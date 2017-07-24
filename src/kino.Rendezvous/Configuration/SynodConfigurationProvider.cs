using System;
using System.Collections.Generic;
using System.Linq;
using kino.Consensus.Configuration;
using kino.Core.Framework;

namespace kino.Rendezvous.Configuration
{
    public class SynodConfigurationProvider : ISynodConfigurationProvider
    {
        public SynodConfigurationProvider(SynodConfiguration config)
        {
            LocalNode = config.LocalNode.ParseAddress();
            Synod = config.Members
                          .Select(m => m.ParseAddress())
                          .ToList();
            HeartBeatInterval = config.HeartBeatInterval;
            MissingHeartBeatsBeforeReconnect = config.MissingHeartBeatsBeforeReconnect;
            IntercomEndpoint = new Uri(config.IntercomEndpoint);
        }

        public Uri LocalNode { get; }

        public IEnumerable<Uri> Synod { get; }

        public TimeSpan HeartBeatInterval { get; }

        public int MissingHeartBeatsBeforeReconnect { get; }

        public Uri IntercomEndpoint { get; }
    }
}