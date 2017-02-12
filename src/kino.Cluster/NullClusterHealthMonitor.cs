using System.Diagnostics.CodeAnalysis;
using kino.Core;

namespace kino.Cluster
{
    [ExcludeFromCodeCoverage]
    public class NullClusterHealthMonitor : IClusterHealthMonitor
    {
        public void StartPeerMonitoring(Node peer, Health health)
        {
        }

        public void AddPeer(Node peer, Health health)
        {
        }

        public void ScheduleConnectivityCheck(ReceiverIdentifier nodeIdentifier)
        {
        }

        public void DeletePeer(ReceiverIdentifier nodeIdentifier)
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