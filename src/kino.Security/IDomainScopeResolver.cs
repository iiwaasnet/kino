using System.Collections.Generic;

namespace kino.Security
{
    public interface IDomainScopeResolver
    {
        IEnumerable<DomainScope> GetDomainMessages(IEnumerable<string> domains);
    }
}