namespace rawf.Actors
{
    public class HostConfiguration : IHostConfiguration
    {
        private readonly string routerAddress;
        public HostConfiguration(string routerAddress)
        {
            this.routerAddress = routerAddress;
        }

        public string GetRouterAddress()
        {
            return routerAddress;
        }
    }
}