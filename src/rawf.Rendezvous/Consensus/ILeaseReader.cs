namespace rawf.Rendezvous.Consensus
{
	public interface ILeaseReader
	{
		LeaseTxResult Read(Ballot ballot);
	}
}