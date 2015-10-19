namespace kino.Messaging
{
    public interface IMessageIdentifier
    {
        byte[] Version { get; }
        byte[] Identity { get; }
    }
}