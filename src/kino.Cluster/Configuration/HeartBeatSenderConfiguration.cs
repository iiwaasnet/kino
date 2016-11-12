using System;
using System.Collections.Generic;

namespace kino.Cluster.Configuration
{
    public class HeartBeatSenderConfiguration
    {
        public IEnumerable<Uri> AddressRange { get; set; }

        public TimeSpan HeartBeatInterval { get; set; }
    }
}