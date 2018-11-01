using System;
using System.Diagnostics.CodeAnalysis;

namespace kino.Cluster.Configuration
{
    [ExcludeFromCodeCoverage]
    public class ServiceLocator<TBase>
    {
        private readonly ClusterMembershipConfiguration config;
        private readonly Func<TBase> real;
        private readonly Func<TBase> @null;

        public ServiceLocator(ClusterMembershipConfiguration config,
                              Func<TBase> real,
                              Func<TBase> @null)
        {
            this.config = config;
            this.real = real;
            this.@null = @null;
        }

        public TBase GetService()
            => config.RunAsStandalone ? @null() : real();
    }
}