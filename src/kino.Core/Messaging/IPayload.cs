namespace kino.Core.Messaging
{
    public interface IPayload : IIdentifier
    {
        T Deserialize<T>(byte[] content);
        byte[] Serialize();
    }
}