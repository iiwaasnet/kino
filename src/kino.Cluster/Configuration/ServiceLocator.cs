using System.Diagnostics.CodeAnalysis;

namespace kino.Cluster.Configuration
{
    [ExcludeFromCodeCoverage]
    public class ServiceLocator<TReal, TNull, TBase>
        where TReal : TBase
        where TNull : TBase
    {
        private readonly ClusterMembershipConfiguration config;
        private readonly TBase real;
        private readonly TBase @null;

        public ServiceLocator(ClusterMembershipConfiguration config,
                              TReal real,
                              TNull @null)
        {
            this.config = config;
            this.real = real;
            this.@null = @null;
        }

        public TBase GetService()
            => config.RunAsStandalone ? @null : real;
    }
}