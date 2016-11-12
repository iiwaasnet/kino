using System;

namespace kino.Consensus
{
    public interface ILeaseProvider : IDisposable
    {
        Lease GetLease(byte[] ownerPayload);

        Lease GetLease();
    }
}