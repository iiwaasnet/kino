using System;

namespace rawf.Consensus
{
    public interface ILease
    {
        byte[] OwnerIdentity { get; }

        DateTime ExpiresAt { get; }
    }
}