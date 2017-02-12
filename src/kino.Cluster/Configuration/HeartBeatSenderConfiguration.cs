using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace kino.Cluster.Configuration
{
    [ExcludeFromCodeCoverage]
    public class HeartBeatSenderConfiguration
    {
        public IEnumerable<Uri> AddressRange { get; set; }

        public TimeSpan HeartBeatInterval { get; set; }
    }
}