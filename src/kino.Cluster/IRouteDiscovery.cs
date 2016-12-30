namespace kino.Cluster
{
    public interface IRouteDiscovery
    {
        void RequestRouteDiscovery(MessageRoute messageRoute);

        void Start();

        void Stop();
    }
}