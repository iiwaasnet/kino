using System.Threading.Tasks;

namespace kino.Core.Connectivity
{
    public interface IRouterConfigurationProvider
    {
        Task<RouterConfiguration> GetRouterConfiguration();

        Task<SocketEndpoint> GetScaleOutAddress();
    }
}