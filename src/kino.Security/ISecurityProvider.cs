using System.Collections.Generic;

namespace kino.Security
{
    public interface ISecurityProvider : ISignatureProvider
    {
        string GetDomain(byte[] MessageIdentity);

        bool DomainIsAllowed(string domain);

        IEnumerable<string> GetAllowedDomains();
    }
}