using System;

namespace rawf.Rendezvous.Consensus
{
    public class Lease : ILease
    {
        public Lease(byte[] ownerIdentity, OwnerEndpoint ownerEndpoint, DateTime expiresAt)
        {
            OwnerIdentity = ownerIdentity;
            OwnerEndpoint = ownerEndpoint;
            ExpiresAt = expiresAt;
        }

        public Lease(byte[] ownerIdentity, OwnerEndpoint ownerEndpoint, long expiresAt)
            : this(ownerIdentity, ownerEndpoint, new DateTime(expiresAt, DateTimeKind.Utc))
        {
        }

        public byte[] OwnerIdentity { get; }
        public DateTime ExpiresAt { get; }
        public OwnerEndpoint OwnerEndpoint { get; }
    }
}