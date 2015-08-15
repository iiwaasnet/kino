using System;

namespace rawf.Consensus
{
    public class Lease : ILease
    {
        public Lease(byte[] ownerIdentity, DateTime expiresAt)
        {
            OwnerIdentity = ownerIdentity;
            ExpiresAt = expiresAt;
        }

        public Lease(byte[] ownerIdentity, long expiresAt)
            :this(ownerIdentity, new DateTime(expiresAt, DateTimeKind.Utc))
        {
        }

        public byte[] OwnerIdentity { get; }
        public DateTime ExpiresAt { get; }
    }
}