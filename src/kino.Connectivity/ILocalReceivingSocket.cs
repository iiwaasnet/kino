using System.Threading;
using kino.Core.Diagnostics.Performance;

namespace kino.Connectivity
{
    public interface ILocalReceivingSocket<T>
    {
        T TryReceive();

        WaitHandle CanReceive();

        IPerformanceCounter ReceiveRate { get; set; }
    }
}