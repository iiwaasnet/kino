using System;
using System.Security.Cryptography;
using Autofac;
using kino.Core.Security;

namespace Client
{
    public class SecurityModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<SecurityProvider>()
                   .As<ISecurityProvider>()
                   .SingleInstance();

            builder.RegisterType<DomainPrivateKeyProvider>()
                   .As<IDomainPrivateKeyProvider>()
                   .SingleInstance();

            builder.RegisterType<DomainScopeResolver>()
                   .As<IDomainScopeResolver>()
                   .SingleInstance();

            builder.Register(m => (Func<HMAC>)(() => HMAC.Create("HMACMD5")))
                   .As<Func<HMAC>>()
                   .SingleInstance();
        }
    }
}