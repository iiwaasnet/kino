using kino.Core.Diagnostics.Performance;

namespace kino.Connectivity
{
    public interface ILocalSendingSocket<in T>
    {
        void Send(T message);

        IPerformanceCounter SendRate { get; set; }
    }
}