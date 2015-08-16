using System;

namespace rawf.Consensus
{
    public class Lease : ILease
    {
        public Lease(Uri ownerUri, byte[] ownerIdentity, DateTime expiresAt)
        {
            OwnerIdentity = ownerIdentity;
            OwnerUri = ownerUri;
            ExpiresAt = expiresAt;
        }

        public Lease(Uri ownerUri, byte[] ownerIdentity, long expiresAt)
            :this(ownerUri, ownerIdentity, new DateTime(expiresAt, DateTimeKind.Utc))
        {
        }

        public byte[] OwnerIdentity { get; }
        public DateTime ExpiresAt { get; }
        public Uri OwnerUri { get; }
    }
}