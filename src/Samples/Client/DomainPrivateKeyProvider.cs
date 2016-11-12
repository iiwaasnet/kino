using System.Collections.Generic;
using System.Linq;
using kino.Core.Framework;
using kino.Security;

namespace Client
{
    class DomainPrivateKeyProvider : IDomainPrivateKeyProvider
    {
        public IEnumerable<DomainPrivateKey> GetAllowedDomainKeys()
        {
            foreach (var ch in "AB".ToCharArray())
            {
                yield return new DomainPrivateKey
                             {
                                 Domain = new string(ch, 30),
                                 PrivateKey = new string(ch, 16).GetBytes().Take(16).ToArray()
                             };
            }
        }
    }
}