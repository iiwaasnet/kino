namespace rawf.Consensus
{
	public interface ILeaseWriter
	{
		ILeaseTxResult Write(IBallot ballot, ILease lease);
	}
}