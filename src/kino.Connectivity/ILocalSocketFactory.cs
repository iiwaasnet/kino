namespace kino.Connectivity
{
    public interface ILocalSocketFactory
    {
        ILocalSocket<T> Create<T>();
    }
}