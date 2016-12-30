using System.Collections.Generic;

namespace kino.Security
{
    public interface IDomainPrivateKeyProvider
    {
        IEnumerable<DomainPrivateKey> GetAllowedDomainKeys();

        Dictionary<string, HashSet<EquatableIdentity>> GetUnsignedDomains();
    }
}