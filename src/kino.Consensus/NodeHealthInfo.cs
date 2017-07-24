using System;

namespace kino.Consensus
{
    internal class NodeHealthInfo
    {
        private readonly object @lock = new object();
        private DateTime lastKnownHeartBeat;

        internal DateTime LastKnownHeartBeat
        {
            get
            {
                lock (@lock)
                {
                    return lastKnownHeartBeat;
                }
            }
            set
            {
                lock (@lock)
                {
                    lastKnownHeartBeat = value;
                }
            }
        }
    }
}