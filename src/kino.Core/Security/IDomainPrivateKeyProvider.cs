using System.Collections.Generic;

namespace kino.Core.Security
{
    public interface IDomainPrivateKeyProvider
    {
        IEnumerable<DomainPrivateKey> GetAllowedDomainKeys();
    }
}