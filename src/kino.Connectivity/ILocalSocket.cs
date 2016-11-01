using kino.Core;

namespace kino.Connectivity
{
    public interface ILocalSocket<T> : ILocalReceivingSocket<T>, ILocalSendingSocket<T>
    {
        SocketIdentifier GetIdentity();
    }
}