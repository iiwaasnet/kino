using System;

namespace rawf.Consensus
{
    public interface ILease
    {
        byte[] OwnerIdentity { get; }
        Uri OwnerUri { get; }
        DateTime ExpiresAt { get; }
    }
}