using kino.Core.Connectivity;

namespace kino.Cluster
{
    public interface IRouteDiscovery
    {
        void RequestRouteDiscovery(Identifier messageIdentifier);

        void Start();

        void Stop();
    }
}