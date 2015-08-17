namespace rawf.Rendezvous.Consensus
{
	public interface ILeaseTxResult
	{
		TxOutcome TxOutcome { get; }
		ILease Lease { get; }
	}
}