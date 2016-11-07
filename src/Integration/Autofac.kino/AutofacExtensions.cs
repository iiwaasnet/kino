using System;
using System.Collections.Generic;
using Autofac.Builder;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core.Diagnostics;

namespace Autofac.kino
{
    public static class AutofacExtensions
    {
        public static IRegistrationBuilder<SocketConfiguration, SimpleActivatorData, SingleRegistrationStyle>
            RegisterSocketConfiguration(this ContainerBuilder builder, Func<IComponentContext, SocketConfiguration> @delegate)
            => builder.Register(@delegate).AsSelf();

        public static IRegistrationBuilder<ClusterMembershipConfiguration, SimpleActivatorData, SingleRegistrationStyle>
            RegisterClusterMembershipConfiguration(this ContainerBuilder builder, Func<IComponentContext, ClusterMembershipConfiguration> @delegate)
            => builder.Register(@delegate).AsSelf();

        public static IRegistrationBuilder<RouterConfiguration, SimpleActivatorData, SingleRegistrationStyle>
            RegisterRouterConfiguration(this ContainerBuilder builder, Func<IComponentContext, RouterConfiguration> @delegate)
            => builder.Register(@delegate).AsSelf();

        public static IRegistrationBuilder<IEnumerable<RendezvousEndpoint>, SimpleActivatorData, SingleRegistrationStyle>
            RegisterRendezvousEndpoints(this ContainerBuilder builder, Func<IComponentContext, IEnumerable<RendezvousEndpoint>> @delegate)
            => builder.Register(@delegate).AsSelf();

        public static IRegistrationBuilder<ILogger, SimpleActivatorData, SingleRegistrationStyle>
            RegisterLogger(this ContainerBuilder builder, Func<IComponentContext, ILogger> @delegate)
            => builder.Register(@delegate).As<ILogger>();
    }
}