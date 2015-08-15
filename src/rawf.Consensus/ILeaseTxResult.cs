namespace rawf.Consensus
{
	public interface ILeaseTxResult
	{
		TxOutcome TxOutcome { get; }
		ILease Lease { get; }
	}
}