using Autofac;
using Autofac.Configuration;
using kino.Rendezvous.Configuration;
using Topshelf;
using Topshelf.HostConfigurators;

namespace kino.Rendezvous
{
    public class ServiceHost
    {
        public static void Run()
            => HostFactory.Run(CreateServiceConfiguration);

        private static void CreateServiceConfiguration(HostConfigurator x)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new MainModule());
            builder.RegisterModule(new ConfigurationSettingsReader("autofac"));
            var container = builder.Build();

            var config = container.Resolve<RendezvousConfiguration>();

            x.Service<IRendezvousService>(s =>
                                          {
                                              s.ConstructUsing(c => CreateServiceInstance(container));
                                              s.WhenStarted(rs => rs.Start());
                                              s.WhenStopped(rs => rs.Stop());
                                          });
            x.RunAsPrompt();
            x.SetServiceName(config.ServiceName);
            x.SetDisplayName(config.ServiceName);
            x.SetDescription($"{config.ServiceName} Service");
        }

        private static IRendezvousService CreateServiceInstance(IContainer container)
            => container.Resolve<IRendezvousService>();
    }
}