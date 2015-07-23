using System;
using System.Collections.Generic;

namespace rawf.Connectivity
{
    public interface IAdsConfiguration
    {
        IEnumerable<AdsServerConfiguration> GetAdsServers();
    }
}