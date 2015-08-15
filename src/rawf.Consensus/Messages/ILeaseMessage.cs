namespace rawf.Consensus.Messages
{
    public interface ILeaseMessage
    {
        Ballot Ballot { get; }
        string Uri { get; }
    }
}