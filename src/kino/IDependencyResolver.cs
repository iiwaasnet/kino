namespace kino
{
    public interface IDependencyResolver
    {
        T Resolve<T>();
    }
}