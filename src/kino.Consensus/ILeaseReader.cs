namespace kino.Consensus
{
	public interface ILeaseReader
	{
		LeaseTxResult Read(Ballot ballot);
	}
}