using System.Collections.Generic;

namespace kino.Core.Security
{
    public interface ISecurityProvider : ISignatureProvider
    {
        //byte[] CreateDomainSignature(MessageIdentifier identity, Message message);
        //byte[] CreateDomainSignature(string domain, Message message);
        //byte[] CreateOwnedDomainSignature(Message message);

        //void VerifyOwnedDomainSignature(Message message);
        //void VerifyDomainSignature(Message message, string domain);
        //void VerifyDomainSignature(Message message, MessageIdentifier identity);

        //string GetOwnedDomain();
        //string GetDomain(MessageIdentifier identity);

        string GetSecurityDomain(byte[] messageIdentity);
        bool SecurityDomainIsAllowed(string domain);
        IEnumerable<string> GetAllowedSecurityDomains();
    }
}