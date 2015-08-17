namespace rawf.Rendezvous.Consensus
{
	public class LeaseTxResult : ILeaseTxResult
	{
		public TxOutcome TxOutcome { get; set; }
		public ILease Lease { get; set; }
	}
}