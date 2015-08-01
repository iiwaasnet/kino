using rawf.Connectivity;

namespace rawf.Actors
{
    public interface IRouterConfigurationProvider
    {
        IRouterConfiguration GetConfiguration();
    }
}