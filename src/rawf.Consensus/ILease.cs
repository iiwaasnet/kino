using System;

namespace rawf.Consensus
{
    public interface ILease
    {
        byte[] OwnerIdentity { get; }
        //TODO: Should point to broadcast and unicast URIs in order to force proper reconnect to Leader
        Uri OwnerUri { get; }
        DateTime ExpiresAt { get; }
    }
}