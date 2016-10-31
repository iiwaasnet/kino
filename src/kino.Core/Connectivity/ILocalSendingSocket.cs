namespace kino.Core.Connectivity
{
    public interface ILocalSendingSocket<in T>
    {
        void Send(T message);
    }
}