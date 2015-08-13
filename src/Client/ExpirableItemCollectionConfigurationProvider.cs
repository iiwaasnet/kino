using rawf.Framework;

namespace Client
{
    public class ExpirableItemCollectionConfigurationProvider : IExpirableItemCollectionConfigurationProvider
    {
        private readonly IExpirableItemCollectionConfiguration config;

        public ExpirableItemCollectionConfigurationProvider(ApplicationConfiguration appConfig)
        {
            config = new ExpirableItemCollectionConfiguration {EvaluationInterval = appConfig.PromiseExpirationEvaluationInterval};
        }

        public IExpirableItemCollectionConfiguration GetConfiguration()
            => config;
    }
}