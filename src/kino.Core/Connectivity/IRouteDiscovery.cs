using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public interface IRouteDiscovery
    {
        void RequestRouteDiscovery(Identifier messageIdentifier);

        void Start();

        void Stop();
    }
}