using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace kino.Configuration
{
    public class HeartBeatSenderConfigurationManager : IHeartBeatSenderConfigurationManager
    {
        private readonly HeartBeatSenderConfiguration config;
        private readonly TaskCompletionSource<Uri> heartBeatAddressSource;
        private Uri heartBeatAddress;

        public HeartBeatSenderConfigurationManager(HeartBeatSenderConfiguration config)
        {
            this.config = config;
            heartBeatAddressSource = new TaskCompletionSource<Uri>();
        }

        public Uri GetHeartBeatAddress()
            => heartBeatAddress ?? (heartBeatAddress = heartBeatAddressSource.Task.Result);

        public IEnumerable<Uri> GetHeartBeatAddressRange()
            => config.AddressRange;

        public void SetActiveHeartBeatAddress(Uri activeAddress)
        {
            if (config.AddressRange.Contains(activeAddress))
            {
                heartBeatAddressSource.SetResult(activeAddress);
            }
            else
            {
                throw new Exception($"HeartBeat Uri {activeAddress} is not configured!");
            }
        }

        public TimeSpan GetHeartBeatInterval()
            => config.HeartBeatInterval;
    }
}