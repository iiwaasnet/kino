namespace kino.Connectivity
{
    public class LocalSocketFactory : ILocalSocketFactory
    {
        public ILocalSocket<T> Create<T>()
            => new LocalSocket<T>();
    }
}