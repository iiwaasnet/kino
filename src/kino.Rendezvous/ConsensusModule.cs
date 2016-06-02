using Autofac;
using kino.Consensus;
using kino.Consensus.Configuration;
using kino.Core.Messaging;
using kino.Rendezvous.Configuration;
using SynodConfiguration = kino.Consensus.Configuration.SynodConfiguration;

namespace kino.Rendezvous
{
    public class ConsensusModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            RegisterConfigurations(builder);

            builder.RegisterType<BallotGenerator>()
                   .As<IBallotGenerator>()
                   .SingleInstance();
            builder.RegisterType<SynodConfigurationProvider>()
                   .As<ISynodConfigurationProvider>()
                   .SingleInstance();
            builder.RegisterType<SynodConfiguration>()
                   .As<ISynodConfiguration>()
                   .SingleInstance();
            builder.RegisterType<IntercomMessageHub>()
                   .As<IIntercomMessageHub>()
                   .SingleInstance();
            builder.RegisterType<RoundBasedRegister>()
                   .As<IRoundBasedRegister>()
                   .SingleInstance();
            builder.RegisterType<LeaseProvider>()
                   .As<ILeaseProvider>()
                   .SingleInstance();
            builder.RegisterType<ProtobufMessageSerializer>()
                   .As<IMessageSerializer>()
                   .SingleInstance();
        }

        private static void RegisterConfigurations(ContainerBuilder builder)
        {
            builder.Register(c => c.Resolve<ApplicationConfiguration>().Lease)
                   .As<LeaseConfiguration>()
                   .SingleInstance();
            builder.Register(c => c.Resolve<ApplicationConfiguration>().Synod)
                   .As<Configuration.SynodConfiguration>()
                   .SingleInstance();
            builder.Register(c => c.Resolve<ApplicationConfiguration>().Rendezvous)
                   .As<RendezvousConfiguration>()
                   .SingleInstance();
        }
    }
}