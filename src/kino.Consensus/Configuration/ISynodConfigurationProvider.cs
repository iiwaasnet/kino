using System;
using System.Collections.Generic;

namespace kino.Consensus.Configuration
{
    public interface ISynodConfigurationProvider
    {
        Uri LocalNode { get; }
        IEnumerable<Uri> Synod { get; }
    }
}