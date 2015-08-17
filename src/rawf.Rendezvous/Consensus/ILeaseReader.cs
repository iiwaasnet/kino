namespace rawf.Rendezvous.Consensus
{
	public interface ILeaseReader
	{
		ILeaseTxResult Read(IBallot ballot);
	}
}