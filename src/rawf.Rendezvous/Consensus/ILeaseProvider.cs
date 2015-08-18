using System;

namespace rawf.Rendezvous.Consensus
{
	public interface ILeaseProvider : IDisposable
	{
		Lease GetLease();
	    void ResetLease();
	}
}