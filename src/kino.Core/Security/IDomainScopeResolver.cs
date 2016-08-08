using System.Collections.Generic;

namespace kino.Core.Security
{
    public interface IDomainScopeResolver
    {
        IEnumerable<DomainScope> GetDomainMessages(IEnumerable<string> domains);
    }
}