using kino.Core;

namespace kino.Cluster
{
    public interface IClusterHealthMonitor
    {
        void StartPeerMonitoring(Node peer, Health health);

        void ScheduleConnectivityCheck(ReceiverIdentifier nodeIdentifier);

        void AddPeer(Node peer, Health health);

        void DeletePeer(ReceiverIdentifier nodeIdentifier);

        void Start();

        void Stop();
    }
}