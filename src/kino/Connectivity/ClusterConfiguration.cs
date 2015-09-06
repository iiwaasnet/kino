using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using kino.Diagnostics;
using kino.Framework;

namespace kino.Connectivity
{
    public class ClusterConfiguration : IClusterConfiguration
    {
        private readonly ConcurrentDictionary<SocketEndpoint, ClusterMemberMeta> clusterMembers;
        private readonly ILogger logger;
        private DateTime lastPingTime;
        private readonly ClusterTimingConfiguration timingConfiguration;

        public ClusterConfiguration(ClusterTimingConfiguration timingConfiguration,  ILogger logger)
        {
            lastPingTime = DateTime.UtcNow;
            this.timingConfiguration = timingConfiguration;
            this.logger = logger;
            clusterMembers = new ConcurrentDictionary<SocketEndpoint, ClusterMemberMeta>();
        }

        public IEnumerable<SocketEndpoint> GetClusterMembers()
            => clusterMembers.Keys;

        public void AddClusterMember(SocketEndpoint node)
        {
            if (clusterMembers.TryAdd(node, new ClusterMemberMeta {LastKnownPong = DateTime.UtcNow}))
            {
                logger.Debug($"New node added " +
                             $"Uri:{node.Uri.AbsoluteUri} " +
                             $"Socket:{node.Identity.GetString()}");
            }
        }

        public bool KeepAlive(SocketEndpoint node)
        {
            ClusterMemberMeta meta;
            var updated = clusterMembers.TryGetValue(node, out meta);
            if (updated)
            {
                meta.LastKnownPong = DateTime.UtcNow;
            }

            return updated;
        }

        public IEnumerable<SocketEndpoint> GetDeadMembers(DateTime pingTime)
        {
            var now = DateTime.UtcNow;
            var pingDelay = CalculatePingDelay(pingTime);
            lastPingTime = pingTime;
            
            return clusterMembers
                .Where(mem => now - mem.Value.LastKnownPong - pingDelay > timingConfiguration.PongSilenceBeforeRouteDeletion)
                .Select(mem => mem.Key)
                .ToList();
        }

        private TimeSpan CalculatePingDelay(DateTime pingTime)
        {
            var pingDelay = pingTime - lastPingTime - timingConfiguration.ExpectedPingInterval;

            return (pingDelay <= TimeSpan.Zero) ? TimeSpan.Zero : pingDelay;
        }

        public void DeleteClusterMember(SocketEndpoint node)
        {
            ClusterMemberMeta meta;
            clusterMembers.TryRemove(node, out meta);

            logger.Debug($"Dead route removed Uri:{node.Uri.AbsoluteUri} " +
                             $"Socket:{node.Identity.GetString()}");
        }
    }
}