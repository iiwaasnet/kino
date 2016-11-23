using System.Collections.Generic;
using kino.Core;

namespace kino.Cluster
{
    public class ClusterServices : IClusterServices
    {
        private readonly IClusterMonitor clusterMonitor;
        private readonly IScaleOutListener scaleOutListener;
        private readonly IHeartBeatSender heartBeatSender;
        private readonly IClusterHealthMonitor clusterHealthMonitor;

        public ClusterServices(IClusterMonitor clusterMonitor,
                               IScaleOutListener scaleOutListener,
                               IHeartBeatSender heartBeatSender,
                               IClusterHealthMonitor clusterHealthMonitor)
        {
            this.clusterMonitor = clusterMonitor;
            this.scaleOutListener = scaleOutListener;
            this.heartBeatSender = heartBeatSender;
            this.clusterHealthMonitor = clusterHealthMonitor;
        }

        public void DiscoverMessageRoute(Identifier messageIdentifier)
            => clusterMonitor.DiscoverMessageRoute(messageIdentifier);

        public void RegisterSelf(IEnumerable<MessageRoute> registrations, string domain)
            => clusterMonitor.RegisterSelf(registrations, domain);

        public void UnregisterSelf(IEnumerable<MessageRoute> registrations)
            => clusterMonitor.UnregisterSelf(registrations);

        public void StartPeerMonitoring(Node node, Health health)
            => clusterHealthMonitor.StartPeerMonitoring(node, health);

        public void AddPeer(Node peer, Health health)
            => clusterHealthMonitor.AddPeer(peer, health);

        public void DeletePeer(ReceiverIdentifier socketIdentifier)
            => clusterHealthMonitor.DeletePeer(socketIdentifier);

        public void StopClusterServices()
        {
            clusterMonitor.Stop();
            scaleOutListener.Stop();
            heartBeatSender.Stop();
            clusterHealthMonitor.Stop();
        }

        public void StartClusterServices()
        {
            clusterMonitor.Start();
            scaleOutListener.Start();
            heartBeatSender.Start();
            clusterHealthMonitor.Start();
        }
    }
}