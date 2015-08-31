namespace kino.Rendezvous.Consensus
{
	public interface ILeaseWriter
	{
		LeaseTxResult Write(Ballot ballot, Lease lease);
	}
}