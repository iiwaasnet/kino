using kino.Core.Connectivity;

namespace kino.Messaging
{
    public interface IPayload : IIdentifier
    {
        T Deserialize<T>(byte[] content);
        byte[] Serialize();
    }
}