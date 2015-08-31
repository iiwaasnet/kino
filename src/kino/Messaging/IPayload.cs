namespace kino.Messaging
{
    public interface IPayload
    {
        T Deserialize<T>(byte[] content);
        byte[] Serialize();
    }
}