using System;

namespace kino.Cluster.Configuration
{
    public interface IHeartBeatSenderConfigurationProvider
    {
        string GetHeartBeatAddress();

        TimeSpan GetHeartBeatInterval();
    }
}