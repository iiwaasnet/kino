using Autofac;

namespace rawf.Actors
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(new rawf.MainModule());

            builder.RegisterType<ActorHost>()
                   .As<IActorHost>()
                   .SingleInstance();

            builder.RegisterType<ActorHandlerMap>()
                   .As<IActorHandlerMap>()
                   .SingleInstance();

            builder.RegisterType<MessagesCompletionQueue>()
                   .As<IMessagesCompletionQueue>()
                   .SingleInstance();
        }
    }
}