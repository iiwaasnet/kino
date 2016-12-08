﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using kino.Core.Framework;

namespace kino.Security
{
    public class SecurityProvider : ISecurityProvider
    {
        private readonly IDictionary<string, DomainPrivateKey> nameToDomainMap;
        private readonly IDictionary<AnyVersionMessageIdentifier, DomainPrivateKey> messageToDomainMap;
        private readonly KeyedHashAlgorithm mac;
        private readonly object @lock = new object();

        public SecurityProvider(Func<HMAC> macImplFactory,
                                IDomainScopeResolver domainScopeResolver,
                                IDomainPrivateKeyProvider domainPrivateKeyProvider)
        {
            nameToDomainMap = domainPrivateKeyProvider.GetAllowedDomainKeys()
                                                      .ToDictionary(dk => dk.Domain, dk => dk);
            messageToDomainMap = CreateMessageMapping(nameToDomainMap, domainScopeResolver);
            mac = macImplFactory();
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
        {
            DomainPrivateKey domain;
            if (messageToDomainMap.TryGetValue(identity, out domain))
            {
                return domain;
            }
            throw new MessageNotSupportedException(identity.ToString());
        }

        private DomainPrivateKey FindDomain(string name)
        {
            DomainPrivateKey domain;
            if (nameToDomainMap.TryGetValue(name, out domain))
            {
                return domain;
            }

            throw new SecurityException($"Domain {name} is not allowed!");
        }

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
                DomainPrivateKey key, _;
                if (domainKeys.TryGetValue(message.Domain, out key))
                {
                    var messageIdentifier = new AnyVersionMessageIdentifier(message.Identity.GetBytes());
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
    }
}