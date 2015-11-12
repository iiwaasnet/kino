using System;

namespace kino.Consensus
{
    public class Lease
    {
        public Lease(byte[] ownerIdentity, DateTime expiresAt, byte[] ownerPayload)
        {
            OwnerIdentity = ownerIdentity;
            OwnerPayload = ownerPayload;
            ExpiresAt = expiresAt;
        }

        public Lease(byte[] ownerIdentity, long expiresAt, byte[] ownerPayload)
            : this(ownerIdentity, new DateTime(expiresAt, DateTimeKind.Utc), ownerPayload)
        {
        }

        public byte[] OwnerIdentity { get; }
        public DateTime ExpiresAt { get; }
        public byte[] OwnerPayload { get; }
    }
}