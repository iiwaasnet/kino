namespace rawf.Rendezvous.Consensus
{
	public interface ILeaseWriter
	{
		ILeaseTxResult Write(IBallot ballot, ILease lease);
	}
}