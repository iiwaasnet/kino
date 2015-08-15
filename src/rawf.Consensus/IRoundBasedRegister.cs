using System;

namespace rawf.Consensus
{
	public interface IRoundBasedRegister : ILeaseReader, ILeaseWriter, IDisposable
	{
	}
}