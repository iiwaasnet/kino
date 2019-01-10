namespace kino.Connectivity
{
    public interface ISendingSocket<in T>
    {
        void Send(T message);
    }
}