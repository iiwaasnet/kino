using System;
using kino.Core;
using kino.Core.Framework;

namespace kino.Consensus
{
    internal class NodeHealthInfo : INodeHealthInfo
    {
        private readonly TimeSpan heartBeatInterval;
        private readonly int missingHeartBeatsBeforeReconnect;
        private readonly DynamicUri dynamicUri;
        private readonly object @lock = new object();
        private DateTime lastKnownHeartBeat;
        private DateTime lastReconnectAttempt;

        public NodeHealthInfo(TimeSpan heartBeatInterval,
                              int missingHeartBeatsBeforeReconnect,
                              DynamicUri dynamicUri)
        {
            this.heartBeatInterval = heartBeatInterval;
            this.missingHeartBeatsBeforeReconnect = missingHeartBeatsBeforeReconnect;
            this.dynamicUri = dynamicUri;
            UpdateLastReconnectTime();
        }

        internal void UpdateHeartBeat()
        {
            lock (@lock)
            {
                lastKnownHeartBeat = DateTime.UtcNow;
            }
        }

        internal void UpdateLastReconnectTime()
        {
            lock (@lock)
            {
                lastReconnectAttempt = DateTime.UtcNow;
            }
        }

        public bool IsHealthy()
        {
            lock (@lock)
            {
                return DateTime.UtcNow - lastKnownHeartBeat < heartBeatInterval.MultiplyBy(missingHeartBeatsBeforeReconnect);
            }
        }

        internal bool ShouldReconnect()
        {
            lock (@lock)
            {
                return DateTime.UtcNow - lastReconnectAttempt >= heartBeatInterval.MultiplyBy(missingHeartBeatsBeforeReconnect);
            }
        }

        public string NodeUri => dynamicUri.Uri;

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

        public DateTime LastReconnectAttempt
        {
            get
            {
                lock (@lock)
                {
                    return lastReconnectAttempt;
                }
            }
        }
    }
}