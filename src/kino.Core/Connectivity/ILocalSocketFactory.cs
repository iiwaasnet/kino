namespace kino.Core.Connectivity
{
    public interface ILocalSocketFactory
    {
        ILocalSocket<T> Create<T>();
    }
}