using System;

namespace kino.Rendezvous.Consensus
{
	public interface ILeaseProvider : IDisposable
	{
		Lease GetLease(byte[] ownerPayload);
		Lease GetLease();
	}
}