using Autofac;
using kino.Connectivity;
using kino.Framework;

namespace kino.Actors
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(new kino.MainModule());

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