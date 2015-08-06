namespace rawf.Connectivity
{
    public class RouterConfiguration : IRouterConfiguration
    {
        public SocketEndpoint RouterAddress { get; set; }
        public SocketEndpoint ScaleOutAddress { get; set; }
    }
}