namespace kino.Rendezvous.Consensus.Messages
{
    public interface ILeaseMessage
    {
        Ballot Ballot { get; }
        string SenderUri { get; }
    }
}