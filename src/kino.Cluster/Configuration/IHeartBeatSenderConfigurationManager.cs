using System;
using System.Collections.Generic;

namespace kino.Cluster.Configuration
{
    public interface IHeartBeatSenderConfigurationManager : IHeartBeatSenderConfigurationProvider
    {
        IEnumerable<Uri> GetHeartBeatAddressRange();

        void SetActiveHeartBeatAddress(Uri activeAddress);
    }
}