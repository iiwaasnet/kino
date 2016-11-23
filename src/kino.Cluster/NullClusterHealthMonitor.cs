using kino.Core;

namespace kino.Cluster
{
    public class NullClusterHealthMonitor : IClusterHealthMonitor
    {
        public void StartPeerMonitoring(Node peer, Health health)
        {
        }

        public void AddPeer(Node peer, Health health)
        {
        }

        public void DeletePeer(ReceiverIdentifier socketIdentifier)
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }
}