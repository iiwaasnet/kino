using System;
using System.Collections.Generic;
using Autofac.Builder;
using kino.Client;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Sockets;

namespace Autofac.kino
{
    public static class AutofacExtensions
    {
        public static IRegistrationBuilder<SocketConfiguration, SimpleActivatorData, SingleRegistrationStyle>
            RegisterSocketConfiguration(this ContainerBuilder builder, Func<IComponentContext, SocketConfiguration> @delegate)
        {
            return builder.Register(@delegate).AsSelf();
        }

        public static IRegistrationBuilder<ClusterMembershipConfiguration, SimpleActivatorData, SingleRegistrationStyle>
            RegisterClusterMembershipConfiguration(this ContainerBuilder builder, Func<IComponentContext, ClusterMembershipConfiguration> @delegate)
        {
            return builder.Register(@delegate).AsSelf();
        }

        public static IRegistrationBuilder<RouterConfiguration, SimpleActivatorData, SingleRegistrationStyle>
            RegisterRouterConfiguration(this ContainerBuilder builder, Func<IComponentContext, RouterConfiguration> @delegate)
        {
            return builder.Register(@delegate).AsSelf();
        }

        public static IRegistrationBuilder<IEnumerable<RendezvousEndpoint>, SimpleActivatorData, SingleRegistrationStyle>
            RegisterRendezvousEndpoints(this ContainerBuilder builder, Func<IComponentContext, IEnumerable<RendezvousEndpoint>> @delegate)
        {
            return builder.Register(@delegate).AsSelf();
        }

        public static IRegistrationBuilder<ILogger, SimpleActivatorData, SingleRegistrationStyle>
            RegisterLogger(this ContainerBuilder builder, Func<IComponentContext, ILogger> @delegate)
        {
            return builder.Register(@delegate).As<ILogger>();
        }

        public static IRegistrationBuilder<MessageHubConfiguration, SimpleActivatorData, SingleRegistrationStyle>
            RegisterMessageHubConfiguration(this ContainerBuilder builder, Func<IComponentContext, MessageHubConfiguration> @delegate)
        {
            return builder.Register(@delegate).AsSelf();
        }        
    }
}