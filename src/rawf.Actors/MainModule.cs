using Autofac;
using rawf.Connectivity;
using rawf.Framework;

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

            builder.RegisterType<AsyncQueue<AsyncMessageContext>>()
                   .As<IAsyncQueue<AsyncMessageContext>>()
                   .SingleInstance();

            builder.RegisterType<AsyncQueue<IActor>>()
                   .As<IAsyncQueue<IActor>>()
                   .SingleInstance();

            builder.RegisterType<MessageTracer>()
                   .As<IMessageTracer>()
                   .SingleInstance();
        }
    }
}