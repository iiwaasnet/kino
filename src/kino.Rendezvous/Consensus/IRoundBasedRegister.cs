using System;

namespace kino.Rendezvous.Consensus
{
	public interface IRoundBasedRegister : ILeaseReader, ILeaseWriter, IDisposable
	{
	}
}