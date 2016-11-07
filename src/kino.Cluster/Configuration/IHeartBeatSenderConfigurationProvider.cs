using System;

namespace kino.Cluster.Configuration
{
    public interface IHeartBeatSenderConfigurationProvider
    {
        Uri GetHeartBeatAddress();

        TimeSpan GetHeartBeatInterval();
    }
}