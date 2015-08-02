namespace rawf.Connectivity
{
    public interface IRouterConfiguration
    {
        SocketEndpoint RouterAddress { get; }
        SocketEndpoint ScaleOutAddress { get; }
    }
}