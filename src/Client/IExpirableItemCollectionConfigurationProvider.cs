using rawf.Framework;

namespace Client
{
    public interface IExpirableItemCollectionConfigurationProvider
    {
        IExpirableItemCollectionConfiguration GetConfiguration();
    }
}