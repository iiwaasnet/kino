namespace rawf.Consensus
{
	public interface IBallotGenerator
	{
		IBallot New(byte[] identity);

		IBallot Null();
	}
}