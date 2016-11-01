using kino.Core;

namespace kino.Cluster
{
    public interface IRouteDiscovery
    {
        void RequestRouteDiscovery(Identifier messageIdentifier);

        void Start();

        void Stop();
    }
}