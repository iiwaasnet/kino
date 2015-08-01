using Autofac;
using rawf.Connectivity;
using TypedConfigProvider;

namespace rawf.Actors
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(new rawf.MainModule());

            builder.RegisterType<ConfigProvider>()
                   .As<IConfigProvider>()
                   .SingleInstance();

            builder.RegisterType<AppConfigTargetProvider>()
                   .As<IConfigTargetProvider>()
                   .SingleInstance();

            builder.RegisterType<ActorHost>()
                   .As<IActorHost>()
                   .SingleInstance();

            builder.RegisterType<ActorHandlerMap>()
                   .As<IActorHandlerMap>()
                   .SingleInstance();

            builder.RegisterType<MessagesCompletionQueue>()
                   .As<IMessagesCompletionQueue>()
                   .SingleInstance();

            builder.RegisterType<RouterConfigurationProvider>()
                   .As<IRouterConfigurationProvider>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IRouterConfigurationProvider>().GetConfiguration())
                   .As<IRouterConfiguration>()
                   .SingleInstance();
        }
    }
}