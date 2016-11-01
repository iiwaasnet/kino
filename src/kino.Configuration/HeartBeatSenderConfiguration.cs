using System;
using System.Collections.Generic;

namespace kino.Configuration
{
    public class HeartBeatSenderConfiguration
    {
        public IEnumerable<Uri> AddressRange { get; set; }

        public TimeSpan HeartBeatInterval { get; set; }
    }
}