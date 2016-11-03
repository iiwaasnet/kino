using kino.Core;

namespace kino.Cluster
{
    public interface IClusterHealthMonitor
    {
        void StartPeerMonitoring(SocketIdentifier socketIdentifier, Health health);

        void DeletePeer(SocketIdentifier socketIdentifier);

        void Start();

        void Stop();
    }
}