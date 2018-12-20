namespace kino.Cluster.Configuration
{
    public interface IUpdateableConfiguration<T>
    {
        void Update(T newConfig);
    }
}