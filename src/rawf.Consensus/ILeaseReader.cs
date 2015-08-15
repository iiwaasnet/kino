namespace rawf.Consensus
{
	public interface ILeaseReader
	{
		ILeaseTxResult Read(IBallot ballot);
	}
}