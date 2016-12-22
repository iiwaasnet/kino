using Autofac;
using kino.Security;

namespace Client
{
    public class SecurityModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<NullSecurityProvider>()
                   .As<ISecurityProvider>()
                   .SingleInstance();

            builder.RegisterType<DomainPrivateKeyProvider>()
                   .As<IDomainPrivateKeyProvider>()
                   .SingleInstance();

            builder.RegisterType<DomainScopeResolver>()
                   .As<IDomainScopeResolver>()
                   .SingleInstance();
        }
    }
}