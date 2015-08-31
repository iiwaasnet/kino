using kino.Framework;

namespace Client
{
    public interface IExpirableItemCollectionConfigurationProvider
    {
        IExpirableItemCollectionConfiguration GetConfiguration();
    }
}