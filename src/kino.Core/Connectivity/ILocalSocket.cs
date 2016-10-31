namespace kino.Core.Connectivity
{
    public interface ILocalSocket<T> : ILocalReceivingSocket<T>, ILocalSendingSocket<T>
    {
        SocketIdentifier GetIdentity();
    }
}