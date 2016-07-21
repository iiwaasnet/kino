using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using Client.Messages;
using kino.Core.Connectivity;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Security;

namespace Client
{
    public class SampleSecurityProvider : ISecurityProvider
    {
        private readonly Domain ownedDomain;
        private readonly Domain serverDomain;
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
            ownedDomain = domains.Second();
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
            encryptMac = new HMACSHA256();
            decryptMac = new HMACSHA256();
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
            }

            return mapping;
        }

        private IDictionary<string, Domain> CreateDomainMapping(IEnumerable<Domain> domains)
            => domains.ToDictionary(d => d.Name, d => d);

        private IEnumerable<Domain> CreateDomains()
        {
            foreach (var ch in "ABCDEFGHIJKLMN".ToCharArray())
            {
                yield return new Domain
                             {
                                 Name = new string(ch, 30),
                                 Key = new string(ch, 30).GetBytes(),
                                 PrivateKey = new string(ch, 16).GetBytes().Take(16).ToArray()
                             };
            }
        }

        public byte[] CreateDomainSignature(MessageIdentifier identity, Message message)
            => CreateDomainSignature(FindDomain(identity), message);

        public byte[] CreateDomainSignature(string domain, Message message)
            => CreateDomainSignature(FindDomain(domain), message);

        private byte[] CreateDomainSignature(Domain domain, Message message)
        {
            lock (encryptLock)
            {
                //encrypt.GenerateIV();
                //encrypt.Key = domain.PrivateKey;
                //var iv = encrypt.IV;
                //using (var encrypter = encrypt.CreateEncryptor())
                //{
                //    using (var cipherStream = new MemoryStream())
                //    {
                //        using (var cryptoStream = new CryptoStream(cipherStream, encrypter, CryptoStreamMode.Write))
                //        {
                //            using (var binaryWriter = new BinaryWriter(cryptoStream))
                //            {
                //                cipherStream.Write(iv, 0, iv.Length);
                                var mac = CreateMAC(message, domain.PrivateKey, encryptMac);
                return mac;
                //                binaryWriter.Write(mac);
                //                cryptoStream.FlushFinalBlock();
                //            }

                //            return cipherStream.ToArray();
                //        }
                //    }
                //}
            }
        }

        public byte[] CreateOwnedDomainSignature(Message message)
            => CreateDomainSignature(ownedDomain, message);

        public void VerifyOwnedDomainSignature(Message message)
            => VerifyDomainSignature(message, ownedDomain);

        public void VerifyDomainSignature(Message message, MessageIdentifier identity)
            => VerifyDomainSignature(message, FindDomain(identity));

        public void VerifyDomainSignature(Message message, string domain)
            => VerifyDomainSignature(message, FindDomain(domain));

        private void VerifyDomainSignature(Message message, Domain domain)
        {
            var signature = message.Signature;
            lock (decryptLock)
            {
                //decrypt.Key = domain.PrivateKey;
                //using (var ms = new MemoryStream(signature))
                //{
                //    var iv = new byte[16];
                //    ms.Read(iv, 0, 16);
                //    decrypt.IV = iv;

                //    using (var decryptor = decrypt.CreateDecryptor(decrypt.Key, iv))
                //    {
                //        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                //        {
                //            using (var sr = new BinaryReader(cs))
                //            {
                //                var decrypted = sr.ReadBytes(signature.Length - iv.Length);
                                var mac = CreateMAC(message, domain.PrivateKey, decryptMac);
                                if (!Unsafe.Equals(message.Signature, mac))
                                {
                                    throw new WrongMessageSignatureException();
                                }
                //            }
                //        }
                //    }
                //}
            }
        }

        public string GetOwnedDomain()
            => ownedDomain.Name;

        public string GetDomain(MessageIdentifier identity)
            => FindDomain(identity).Name;

        public bool SecurityDomainIsAllowed(string domain)
            => nameToDomainMap.ContainsKey(domain);

        public IEnumerable<string> GetAllowedSecurityDomains()
            => nameToDomainMap.Keys;

        private Domain FindDomain(MessageIdentifier identity)
        {
            Domain domain;
            if (messageToDomainMap.TryGetValue(identity, out domain))
            {
                return domain;
            }
            throw new MessageNotSupportedException();
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

        private static byte[] CreateMAC(Message message, byte[] privateKey, HashAlgorithm mac)
        {
            mac.As<HMACSHA256>().Key = privateKey;
            using (var stream = new MemoryStream(DefaultMACBufferSize))
            {
                stream.Write(message.Identity, 0, message.Identity.Length);
                stream.Write(message.Version, 0, message.Version.Length);
                stream.Write(message.Partition, 0, message.Partition.Length);
                stream.Write(message.Body, 0, message.Body.Length);
                var callbackReceiverIdentity = message.CallbackReceiverIdentity ?? IdentityExtensions.Empty;
                stream.Write(callbackReceiverIdentity, 0, callbackReceiverIdentity.Length);

                stream.Seek(0, SeekOrigin.Begin);

                return mac.ComputeHash(stream);
            }
        }
    }

    internal class Domain
    {
        internal string Name { get; set; }

        internal byte[] PrivateKey { get; set; }

        internal byte[] Key { get; set; }
    }
}