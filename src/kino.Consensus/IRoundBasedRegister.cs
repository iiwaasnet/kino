using System;

namespace kino.Consensus
{
	public interface IRoundBasedRegister : ILeaseReader, ILeaseWriter, IDisposable
	{
	}
}