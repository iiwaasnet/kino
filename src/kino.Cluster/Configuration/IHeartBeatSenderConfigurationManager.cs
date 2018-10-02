using System;
using System.Collections.Generic;

namespace kino.Cluster.Configuration
{
    public interface IHeartBeatSenderConfigurationManager : IHeartBeatSenderConfigurationProvider
    {
        IEnumerable<string> GetHeartBeatAddressRange();

        void SetActiveHeartBeatAddress(string activeAddress);
    }
}