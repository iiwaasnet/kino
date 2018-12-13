namespace kino.Connectivity
{
    public interface ILocalSocketFactory
    {
        ILocalSocket<T> Create<T>();

        ILocalSocket<T> CreateNamed<T>(string name);
    }
}