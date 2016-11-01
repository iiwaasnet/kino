using System;
using System.Collections.Generic;

namespace kino.Configuration
{
    public interface IHeartBeatSenderConfigurationManager : IHeartBeatSenderConfigurationProvider
    {
        IEnumerable<Uri> GetHeartBeatAddressRange();

        void SetActiveHeartBeatAddress(Uri activeAddress);
    }
}