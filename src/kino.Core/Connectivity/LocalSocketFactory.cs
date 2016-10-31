namespace kino.Core.Connectivity
{
    public class LocalSocketFactory : ILocalSocketFactory
    {
        public ILocalSocket<T> Create<T>()
            => new LocalSocket<T>();
    }
}