namespace rawf.Rendezvous.Consensus
{
	public interface IBallotGenerator
	{
		IBallot New(byte[] identity);

		IBallot Null();
	}
}