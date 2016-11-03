namespace kino.Cluster
{
    public interface IScaleOutListener
    {
        void Start();

        void Stop();
    }
}