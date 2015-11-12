using Autofac;
using kino.Consensus.Configuration;
using kino.Messaging;

namespace kino.Consensus
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule<kino.MainModule>();

            builder.RegisterType<LeaseProvider>()
                   .As<ILeaseProvider>()
                   .SingleInstance();

            builder.RegisterType<ProtobufMessageSerializer>()
                   .As<IMessageSerializer>()
                   .SingleInstance();

            builder.RegisterType<RoundBasedRegister>()
                   .As<IRoundBasedRegister>()
                   .SingleInstance();

            builder.RegisterType<BallotGenerator>()
                   .As<IBallotGenerator>()
                   .SingleInstance();

            builder.RegisterType<SynodConfiguration>()
                   .As<ISynodConfiguration>()
                   .SingleInstance();

            builder.RegisterType<IntercomMessageHub>()
                   .As<IIntercomMessageHub>()
                   .SingleInstance();
        }
    }
}