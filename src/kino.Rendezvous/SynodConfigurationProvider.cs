using System;
using System.Collections.Generic;
using System.Linq;
using kino.Rendezvous.Consensus;

namespace kino.Rendezvous
{
    public class SynodConfigurationProvider : ISynodConfigurationProvider
    {
        public SynodConfigurationProvider(ApplicationConfiguration config)
        {
            LocalNode = new Uri(config.Synod.LocalNode);
            Synod = config.Synod.Members.Select(m => new Uri(m));
        }

        public Uri LocalNode { get; }
        public IEnumerable<Uri> Synod { get; }
    }
}