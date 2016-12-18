using System.Collections.Generic;
using kino.Core.Framework;

namespace kino.Security
{
    public class NullSecurityProvider : ISecurityProvider
    {
        private static readonly string[] allowedDomains;

        static NullSecurityProvider()
        {
            allowedDomains = new[] {string.Empty};
        }

        public byte[] CreateSignature(string domain, byte[] buffer)
            => IdentityExtensions.Empty;

        public string GetDomain(byte[] messageIdentity)
            => string.Empty;

        public bool DomainIsAllowed(string domain)
            => true;

        public IEnumerable<string> GetAllowedDomains()
            => allowedDomains;

        public bool SignMessages()
            => false;
    }
}