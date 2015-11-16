namespace kino.Core.Framework
{
    public interface IConfigurationStorage<T>
    {
        T Read();
        void Update(T newConfig);
    }
}