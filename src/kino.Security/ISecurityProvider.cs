using System.Collections.Generic;

namespace kino.Security
{
    public interface ISecurityProvider : ISignatureProvider
    {
        string GetDomain(byte[] messageIdentity);

        bool DomainIsAllowed(string domain);

        IEnumerable<string> GetAllowedDomains();
    }
}