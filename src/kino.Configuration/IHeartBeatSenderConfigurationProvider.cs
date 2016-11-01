using System;

namespace kino.Configuration
{
    public interface IHeartBeatSenderConfigurationProvider
    {
        Uri GetHeartBeatAddress();

        TimeSpan GetHeartBeatInterval();
    }
}