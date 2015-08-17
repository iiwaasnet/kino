using System;

namespace rawf.Rendezvous.Consensus
{
	public interface ILeaseProvider : IDisposable
	{
		ILease GetLease();
	    void ResetLease();
	}
}