using System;

namespace rawf.Consensus
{
	public interface ILeaseProvider : IDisposable
	{
		ILease GetLease();
	    void ResetLease();
	}
}