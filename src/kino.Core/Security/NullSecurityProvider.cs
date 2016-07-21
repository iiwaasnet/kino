using System.Collections.Generic;
using kino.Core.Framework;

namespace kino.Core.Security
{
    public class NullSecurityProvider : ISecurityProvider
    {
        private static readonly string[] allowedDomains;

        static NullSecurityProvider()
        {
            allowedDomains = new[] {string.Empty};
        }

        public byte[] CreateSignature(string securityDomain, byte[] buffer)
            => IdentityExtensions.Empty;

        public string GetSecurityDomain(byte[] messageIdentity)
            => string.Empty;

        public bool SecurityDomainIsAllowed(string domain)
            => true;

        public IEnumerable<string> GetAllowedSecurityDomains()
            => allowedDomains;
    }
}