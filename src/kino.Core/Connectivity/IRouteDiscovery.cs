namespace kino.Core.Connectivity
{
    public interface IRouteDiscovery
    {
        void RequestRouteDiscovery(MessageIdentifier messageIdentifier);
        void Start();
        void Stop();
    }
}