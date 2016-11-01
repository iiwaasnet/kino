using System;
using System.Collections.Generic;
using System.Linq;
using kino.Configuration;
using kino.Core;

namespace Autofac.kino
{
    public class ConfigurationProvider : IConfigurationProvider
    {
        private readonly ApplicationConfiguration appConfig;

        public ConfigurationProvider(ApplicationConfiguration appConfig)
        {
            this.appConfig = appConfig;
        }

        public IEnumerable<RendezvousEndpoint> GetRendezvousEndpointsConfiguration()
            => appConfig.RendezvousServers.Select(s => new RendezvousEndpoint(s.UnicastUri, s.BroadcastUri));

        //TODO: Remove
        public RouterConfiguration GetRouterConfiguration()
            => new RouterConfiguration();

        public ScaleOutSocketConfiguration GetScaleOutConfiguration()
        {
            var addressParts = appConfig.ScaleOutAddressUri.Split(":".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (addressParts.Length != 3)
            {
                throw new FormatException(appConfig.ScaleOutAddressUri);
            }
            var host = $"{addressParts[0]}:{addressParts[1]}";
            var ports = GetPortRange(addressParts[2]);
            var socketIdentifier = SocketIdentifier.CreateIdentity();

            return new ScaleOutSocketConfiguration
                   {
                       AddressRange = ports.Select(p => new SocketEndpoint($"{host}:{p}", socketIdentifier))
                                           .ToList()
                   };
        }

        private static IEnumerable<int> GetPortRange(string portRange)
        {
            var ports = portRange.Split("-".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                                 .Select(p => int.Parse(p.Trim()));
            var firstPort = ports.First();
            var lastPort = ports.Skip(1).FirstOrDefault();

            return Enumerable.Range(firstPort, Math.Abs(lastPort - firstPort) + 1);
        }

        public ClusterMembershipConfiguration GetClusterMembershipConfiguration()
            => new ClusterMembershipConfiguration
               {
                   HeartBeatSilenceBeforeRendezvousFailover = appConfig.HeartBeatSilenceBeforeRendezvousFailover,
                   RunAsStandalone = appConfig.RunAsStandalone
               };

        public ClusterMembershipConfiguration GetClusterTimingConfiguration()
            => new ClusterMembershipConfiguration
               {
                   HeartBeatSilenceBeforeRendezvousFailover = appConfig.HeartBeatSilenceBeforeRendezvousFailover,
                   RunAsStandalone = appConfig.RunAsStandalone
               };
    }
}