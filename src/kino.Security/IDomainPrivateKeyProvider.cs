using System.Collections.Generic;

namespace kino.Security
{
    public interface IDomainPrivateKeyProvider
    {
        IEnumerable<DomainPrivateKey> GetAllowedDomainKeys();

        //TODO: Rename to GetUnsignedDomains()
        Dictionary<string, HashSet<EquatableIdentity>> GetUnsignableDomains();
    }
}