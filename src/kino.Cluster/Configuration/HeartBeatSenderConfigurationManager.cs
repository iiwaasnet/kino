using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using kino.Core.Framework;

namespace kino.Cluster.Configuration
{
    public class HeartBeatSenderConfigurationManager : IHeartBeatSenderConfigurationManager
    {
        private readonly TimeSpan heartBeatInterval;
        private readonly TaskCompletionSource<string> heartBeatAddressSource;
        private string heartBeatAddress;
        private readonly IEnumerable<string> addressRange;

        public HeartBeatSenderConfigurationManager(HeartBeatSenderConfiguration config)
        {
            heartBeatInterval = config.HeartBeatInterval;
            addressRange = config.AddressRange
                                 .Select(a => a.ToSocketAddress())
                                 .ToList();
            heartBeatAddressSource = new TaskCompletionSource<string>();
        }

        public string GetHeartBeatAddress()
            => heartBeatAddress ?? (heartBeatAddress = heartBeatAddressSource.Task.Result);

        public IEnumerable<string> GetHeartBeatAddressRange()
            => addressRange;

        public void SetActiveHeartBeatAddress(string activeAddress)
        {
            if (addressRange.Contains(activeAddress))
            {
                heartBeatAddressSource.SetResult(activeAddress);
            }
            else
            {
                throw new Exception($"HeartBeat Uri {activeAddress} is not configured!");
            }
        }

        public TimeSpan GetHeartBeatInterval()
            => heartBeatInterval;
    }
}