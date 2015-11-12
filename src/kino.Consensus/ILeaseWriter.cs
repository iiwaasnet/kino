namespace kino.Consensus
{
	public interface ILeaseWriter
	{
		LeaseTxResult Write(Ballot ballot, Lease lease);
	}
}