using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using kino.Core.Framework;

namespace kino.Security
{
    public class SecurityProvider : ISecurityProvider
    {
        private readonly bool disableSigning;
        private readonly IDictionary<string, DomainPrivateKey> nameToDomainMap;
        private readonly IDictionary<AnyVersionMessageIdentifier, DomainPrivateKey> messageToDomainMap;
        private readonly KeyedHashAlgorithm mac;
        private readonly object @lock = new object();
        private readonly Dictionary<string, HashSet<EquatableIdentity>> unsignableDomains;

        public SecurityProvider(Func<HMAC> macImplFactory,
                                IDomainScopeResolver domainScopeResolver,
                                IDomainPrivateKeyProvider domainPrivateKeyProvider,
                                bool disableSigning = false)
        {
            this.disableSigning = disableSigning;
            nameToDomainMap = domainPrivateKeyProvider.GetAllowedDomainKeys()
                                                      .ToDictionary(dk => dk.Domain, dk => dk);
            messageToDomainMap = CreateMessageMapping(nameToDomainMap, domainScopeResolver);
            mac = macImplFactory();
            unsignableDomains = domainPrivateKeyProvider.GetUnsignedDomains()
                             ?? new Dictionary<string, HashSet<EquatableIdentity>>();
        }

        public byte[] CreateSignature(string domain, byte[] buffer)
            => CreateDomainSignature(FindDomain(domain), buffer);

        public string GetDomain(byte[] messageIdentity)
            => FindDomain(new AnyVersionMessageIdentifier(messageIdentity)).Domain;

        public bool DomainIsAllowed(string domain)
            => nameToDomainMap.ContainsKey(domain);

        public IEnumerable<string> GetAllowedDomains()
            => nameToDomainMap.Keys;

        private byte[] CreateDomainSignature(DomainPrivateKey domain, byte[] buffer)
        {
            lock (@lock)
            {
                mac.Key = domain.PrivateKey;
                return mac.ComputeHash(buffer, 0, buffer.Length);
            }
        }

        private DomainPrivateKey FindDomain(AnyVersionMessageIdentifier identity)
            => messageToDomainMap.TryGetValue(identity, out var domain)
                   ? domain
                   : throw new MessageNotSupportedException(identity.ToString());

        private DomainPrivateKey FindDomain(string name)
            => nameToDomainMap.TryGetValue(name, out var domain)
                   ? domain
                   : throw new SecurityException($"Domain {name} is not allowed!");

        private static IDictionary<AnyVersionMessageIdentifier, DomainPrivateKey> CreateMessageMapping(IDictionary<string, DomainPrivateKey> domainKeys,
                                                                                                       IDomainScopeResolver domainScopeResolver)
        {
            var mappings = new Dictionary<AnyVersionMessageIdentifier, DomainPrivateKey>();
            var domainScopes = domainScopeResolver.GetDomainMessages(domainKeys.Select(dk => dk.Key));

            foreach (var message in domainScopes.SelectMany(dm => dm.MessageIdentities,
                                                            (dm, id) => new
                                                                        {
                                                                            Identity = id,
                                                                            Domain = dm.Domain
                                                                        }))
            {
                if (domainKeys.TryGetValue(message.Domain, out var key))
                {
                    var messageIdentifier = new AnyVersionMessageIdentifier(message.Identity.GetBytes());
                    DomainPrivateKey _;
                    if (!mappings.TryGetValue(messageIdentifier, out _))
                    {
                        mappings.Add(messageIdentifier, key);
                    }
                    else
                    {
                        throw new Exception($"Message {message.Identity} is already mapped to Domain {_.Domain}!");
                    }
                }
                else
                {
                    throw new Exception($"PrivateKey for Domain {message.Domain} is not found!");
                }
            }

            return mappings;
        }

        public bool ShouldSignMessage(string domain, byte[] identity)
        {
            if (disableSigning)
            {
                return false;
            }

            if (unsignableDomains.TryGetValue(domain, out var messages))
            {
                if (messages == null
                 || !messages.Any()
                 || messages.Contains(new EquatableIdentity(identity)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}