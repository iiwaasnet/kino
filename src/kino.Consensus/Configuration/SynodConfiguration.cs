using System;
using System.Collections.Generic;

namespace kino.Consensus.Configuration
{
    public class SynodConfiguration
    {
        public string LocalNode { get; set; }

        public IEnumerable<string> Members { get; set; }

        public TimeSpan HeartBeatInterval { get; set; }

        public int MissingHeartBeatsBeforeReconnect { get; set; }

        public string IntercomEndpoint { get; set; }
    }
}