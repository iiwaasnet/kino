﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using kino.Core.Connectivity;
using kino.Core.Framework;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using Server.Messages;

namespace Server
{
    public class SampleSecurityProvider : ISecurityProvider
    {
        private readonly Domain serverDomain;
        private readonly Domain kinoDomain;
        private readonly IDictionary<string, Domain> nameToDomainMap;
        private readonly IDictionary<MessageIdentifier, Domain> messageToDomainMap;
        private readonly AesCryptoServiceProvider encrypt;
        private readonly AesCryptoServiceProvider decrypt;
        private readonly HashAlgorithm encryptMac;
        private readonly HashAlgorithm decryptMac;
        private readonly object encryptLock = new object();
        private readonly object decryptLock = new object();
        private const int DefaultMACBufferSize = 2 * 1024;

        public SampleSecurityProvider()
        {
            var domains = CreateDomains();
            serverDomain = domains.First();
            kinoDomain = domains.Second();
            nameToDomainMap = CreateDomainMapping(domains);
            messageToDomainMap = CreateMessageMapping(domains);
            encrypt = new AesCryptoServiceProvider
                      {
                          Mode = CipherMode.CBC,
                          Padding = PaddingMode.PKCS7
                      };
            decrypt = new AesCryptoServiceProvider
                      {
                          Mode = CipherMode.CBC,
                          Padding = PaddingMode.PKCS7
                      };
            encryptMac = new HMACMD5();
            decryptMac = new HMACMD5();
        }

        private IDictionary<MessageIdentifier, Domain> CreateMessageMapping(IEnumerable<Domain> domains)
        {
            var mapping = new Dictionary<MessageIdentifier, Domain>();

            foreach (var domain in domains)
            {
                for (var i = 0; i < 30; i++)
                {
                    mapping[new MessageIdentifier(Guid.NewGuid().ToByteArray())] = domain;
                }
                if (domain.Name == serverDomain.Name)
                {
                    mapping[new MessageIdentifier(new EhlloMessage().Identity)] = domain;
                    mapping[new MessageIdentifier(new GroupCharsResponseMessage().Identity)] = domain;
                    mapping[new MessageIdentifier(new HelloMessage().Identity)] = domain;
                }
                if (domain.Name == kinoDomain.Name)
                {
                    mapping[new MessageIdentifier(KinoMessages.Pong.Identity)] = domain;
                    mapping[new MessageIdentifier(KinoMessages.RegisterInternalMessageRoute.Identity)] = domain;
                    mapping[new MessageIdentifier(KinoMessages.DiscoverMessageRoute.Identity)] = domain;
                    mapping[new MessageIdentifier(KinoMessages.Exception.Identity)] = domain;
                    mapping[new MessageIdentifier(KinoMessages.RegisterExternalMessageRoute.Identity)] = domain;
                    mapping[new MessageIdentifier(KinoMessages.RequestClusterMessageRoutes.Identity)] = domain;
                    mapping[new MessageIdentifier(KinoMessages.RequestNodeMessageRoutes.Identity)] = domain;
                    mapping[new MessageIdentifier(KinoMessages.UnregisterMessageRoute.Identity)] = domain;
                    mapping[new MessageIdentifier(KinoMessages.UnregisterNode.Identity)] = domain;
                    mapping[new MessageIdentifier(KinoMessages.UnregisterNode.Identity)] = domain;
                    mapping[new MessageIdentifier(KinoMessages.RegisterInternalMessageRoute.Identity)] = domain;
                    mapping[new MessageIdentifier(KinoMessages.RequestKnownMessageRoutes.Identity)] = domain;
                }
            }

            return mapping;
        }

        private IDictionary<string, Domain> CreateDomainMapping(IEnumerable<Domain> domains)
            => domains.ToDictionary(d => d.Name, d => d);

        private IEnumerable<Domain> CreateDomains()
        {
            foreach (var ch in "AB".ToCharArray())
            {
                yield return new Domain
                             {
                                 Name = new string(ch, 30),
                                 Key = new string(ch, 30).GetBytes(),
                                 PrivateKey = new string(ch, 16).GetBytes().Take(16).ToArray()
                             };
            }
        }

        public byte[] CreateSignature(string domain, byte[] buffer)
            => CreateDomainSignature(FindDomain(domain), buffer);

        public string GetSecurityDomain(byte[] messageIdentity)
            => FindDomain(new MessageIdentifier(messageIdentity)).Name;

        public bool SecurityDomainIsAllowed(string domain)
            => nameToDomainMap.ContainsKey(domain);

        public IEnumerable<string> GetAllowedSecurityDomains()
            => nameToDomainMap.Keys;

        private byte[] CreateDomainSignature(Domain domain, byte[] buffer)
        {
            lock (encryptLock)
            {
                //return IdentityExtensions.Empty;
                var mac = CreateMAC(buffer, domain.PrivateKey, encryptMac);
                return mac;
            }
        }

        //private byte[] Encrypt(Domain domain, Message message)
        //{
        //    lock (encryptLock)
        //    {
        //        encrypt.GenerateIV();
        //        encrypt.Key = domain.PrivateKey;
        //        var iv = encrypt.IV;
        //        using (var encrypter = encrypt.CreateEncryptor())
        //        {
        //            using (var cipherStream = new MemoryStream())
        //            {
        //                using (var cryptoStream = new CryptoStream(cipherStream, encrypter, CryptoStreamMode.Write))
        //                {
        //                    using (var binaryWriter = new BinaryWriter(cryptoStream))
        //                    {
        //                        cipherStream.Write(iv, 0, iv.Length);
        //                        binaryWriter.Write(mac);
        //                        cryptoStream.FlushFinalBlock();
        //                    }

        //                    return cipherStream.ToArray();
        //                }
        //            }
        //        }
        //    }
        //}

        //private void Decrypt(Message message, Domain domain)
        //{
        //    var signature = message.Signature;
        //    lock (decryptLock)
        //    {
        //        decrypt.Key = domain.PrivateKey;
        //        using (var ms = new MemoryStream(signature))
        //        {
        //            var iv = new byte[16];
        //            ms.Read(iv, 0, 16);
        //            decrypt.IV = iv;

        //            using (var decryptor = decrypt.CreateDecryptor(decrypt.Key, iv))
        //            {
        //                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
        //                {
        //                    using (var sr = new BinaryReader(cs))
        //                    {
        //                        var decrypted = sr.ReadBytes(signature.Length - iv.Length);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

        private Domain FindDomain(MessageIdentifier identity)
        {
            Domain domain;
            if (messageToDomainMap.TryGetValue(identity, out domain))
            {
                return domain;
            }
            throw new MessageNotSupportedException(identity.ToString());
        }

        private Domain FindDomain(string name)
        {
            Domain domain;
            if (nameToDomainMap.TryGetValue(name, out domain))
            {
                return domain;
            }

            throw new SecurityException($"Domain {name} is not allowed!");
        }

        private static byte[] CreateMAC(byte[] buffer, byte[] privateKey, HashAlgorithm mac)
        {
            mac.As<HMACMD5>().Key = privateKey;
            return mac.ComputeHash(buffer, 0, buffer.Length);
        }
    }

    internal class Domain
    {
        internal string Name { get; set; }

        internal byte[] PrivateKey { get; set; }

        internal byte[] Key { get; set; }
    }
}