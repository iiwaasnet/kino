namespace kino.Rendezvous
{
    public interface IDependencyResolver
    {
        T Resolve<T>();
    }
}