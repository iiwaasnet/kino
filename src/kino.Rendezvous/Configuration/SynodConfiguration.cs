using System.Collections.Generic;

namespace kino.Rendezvous.Configuration
{
    public class SynodConfiguration
    {
        public string LocalNode { get; set; }

        public IEnumerable<string> Members { get; set; }
    }
}