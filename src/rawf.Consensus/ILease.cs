using System;

namespace rawf.Consensus
{
    public interface ILease
    {
        byte[] OwnerIdentity { get; }
        OwnerEndpoint OwnerEndpoint { get; }
        DateTime ExpiresAt { get; }
    }
}