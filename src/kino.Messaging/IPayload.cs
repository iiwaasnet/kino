using kino.Core;

namespace kino.Messaging
{
    public interface IPayload : IIdentifier
    {
        T Deserialize<T>(byte[] content);

        byte[] Serialize();
    }
}