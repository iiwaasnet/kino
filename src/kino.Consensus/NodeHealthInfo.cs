using System;
using kino.Core.Framework;

namespace kino.Consensus
{
    internal class NodeHealthInfo : INodeHealthInfo
    {
        private readonly TimeSpan heartBeatInterval;
        private readonly int missingHeartBeatsBeforeReconnect;
        private readonly object @lock = new object();
        private DateTime lastKnownHeartBeat;

        public NodeHealthInfo(TimeSpan heartBeatInterval,
                              int missingHeartBeatsBeforeReconnect,
                              Uri nodeUri)
        {
            this.heartBeatInterval = heartBeatInterval;
            this.missingHeartBeatsBeforeReconnect = missingHeartBeatsBeforeReconnect;
            NodeUri = nodeUri;
            UpdateHeartBeat();
        }

        internal void UpdateHeartBeat()
        {
            lock (@lock)
            {
                lastKnownHeartBeat = DateTime.UtcNow;
            }
        }

        public bool IsHealthy()
        {
            lock (@lock)
            {
                return DateTime.UtcNow - lastKnownHeartBeat < heartBeatInterval.MultiplyBy(missingHeartBeatsBeforeReconnect);
            }
        }

        public Uri NodeUri { get; }

        public DateTime LastKnownHeartBeat
        {
            get
            {
                lock (@lock)
                {
                    return lastKnownHeartBeat;
                }
            }
        }
    }
}