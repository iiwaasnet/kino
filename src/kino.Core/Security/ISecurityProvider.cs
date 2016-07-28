using System.Collections.Generic;

namespace kino.Core.Security
{
    public interface ISecurityProvider : ISignatureProvider
    {
        string GetSecurityDomain(byte[] messageIdentity);
        bool SecurityDomainIsAllowed(string domain);
        IEnumerable<string> GetAllowedSecurityDomains();
    }
}