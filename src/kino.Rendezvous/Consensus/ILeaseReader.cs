namespace kino.Rendezvous.Consensus
{
	public interface ILeaseReader
	{
		LeaseTxResult Read(Ballot ballot);
	}
}