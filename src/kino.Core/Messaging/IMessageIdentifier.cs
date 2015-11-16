namespace kino.Core.Messaging
{
    public interface IMessageIdentifier
    {
        byte[] Version { get; }
        byte[] Identity { get; }
    }
}