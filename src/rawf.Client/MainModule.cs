using System.Collections.Generic;
using Autofac;
using rawf.Connectivity;
using TypedConfigProvider;

namespace rawf.Client
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(new rawf.MainModule());

            builder.RegisterType<MessageHub>()
                   .As<IMessageHub>()
                   .SingleInstance();

            builder.RegisterType<CallbackHandlerStack>()
                   .As<ICallbackHandlerStack>()
                   .SingleInstance();
        }
    }
}