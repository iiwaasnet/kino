namespace kino.Cluster.Configuration
{
    public interface IConfigurationStorage<T>
    {
        T Read();

        void Update(T newConfig);
    }
}