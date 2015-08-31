namespace kino.Rendezvous.Consensus
{
	public interface IBallotGenerator
	{
		Ballot New(byte[] identity);

		Ballot Null();
	}
}