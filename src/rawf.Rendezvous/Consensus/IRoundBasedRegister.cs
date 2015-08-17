using System;

namespace rawf.Rendezvous.Consensus
{
	public interface IRoundBasedRegister : ILeaseReader, ILeaseWriter, IDisposable
	{
	}
}