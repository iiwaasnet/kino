using System;
using System.Collections.Generic;

namespace kino.Rendezvous
{
    public class SynodConfiguration
    {
        public string LocalNode { get; set; }
        public IEnumerable<string> Members { get; set; }
        public TimeSpan MaxLeaseTimeSpan { get; set; }
        public TimeSpan ClockDrift { get; set; }
        public TimeSpan MessageRoundtrip { get; set; }
        public TimeSpan NodeResponseTimeout { get; set; }
    }
}