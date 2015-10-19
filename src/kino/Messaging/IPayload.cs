namespace kino.Messaging
{
    public interface IPayload : IMessageIdentifier
    {
        T Deserialize<T>(byte[] content);
        byte[] Serialize();        
    }
}