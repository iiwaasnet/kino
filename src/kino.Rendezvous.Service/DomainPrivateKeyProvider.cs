using System.Collections.Generic;
using System.Linq;
using kino.Core.Framework;
using kino.Security;

namespace kino.Rendezvous.Service
{
    public class DomainPrivateKeyProvider : IDomainPrivateKeyProvider
    {
        private readonly IEnumerable<string> domains;

        public DomainPrivateKeyProvider() 
            => domains = GetDomains();

        public IEnumerable<DomainPrivateKey> GetAllowedDomainKeys()
        {
            foreach (var domain in domains)
            {
                yield return new DomainPrivateKey
                             {
                                 Domain = domain,
                                 PrivateKey = domain.GetBytes().Take(16).ToArray()
                             };
            }
        }

        private IEnumerable<string> GetDomains()
        {
            foreach (var ch in "AB".ToCharArray())
            {
                yield return new string(ch, 30);
            }
        }

        public Dictionary<string, HashSet<EquatableIdentity>> GetUnsignedDomains()
            => domains.ToDictionary(d => d, d => (HashSet<EquatableIdentity>) null);
    }
}