using Autofac;
using rawf.Sockets;
using TypedConfigProvider;

namespace rawf.Rendezvous
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ConfigProvider>()
                   .As<IConfigProvider>()
                   .SingleInstance();

            builder.RegisterType<AppConfigTargetProvider>()
                   .As<IConfigTargetProvider>()
                   .SingleInstance();

            builder.RegisterType<RendezvousService>()
                   .As<IRendezvousService>()
                   .SingleInstance();

            builder.RegisterType<SocketFactory>()
                   .As<ISocketFactory>()
                   .SingleInstance();
        }
    }
}