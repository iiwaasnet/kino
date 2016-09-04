namespace kino.Core.Connectivity
{
    public interface IRouterConfigurationProvider
    {
        RouterConfiguration GetRouterConfiguration();

        SocketEndpoint GetScaleOutAddress();
    }
}