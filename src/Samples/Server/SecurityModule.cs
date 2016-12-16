using Autofac;
using Client;
using kino.Security;

namespace Server
{
    public class SecurityModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<DomainPrivateKeyProvider>()
                   .As<IDomainPrivateKeyProvider>()
                   .SingleInstance();

            builder.RegisterType<DomainScopeResolver>()
                   .As<IDomainScopeResolver>()
                   .SingleInstance();
        }
    }
}