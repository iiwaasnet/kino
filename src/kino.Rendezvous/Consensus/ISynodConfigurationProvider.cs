using System;
using System.Collections.Generic;

namespace kino.Rendezvous.Consensus
{
    public interface ISynodConfigurationProvider
    {
        Uri LocalNode { get; }
        IEnumerable<Uri> Synod { get; }
    }
}