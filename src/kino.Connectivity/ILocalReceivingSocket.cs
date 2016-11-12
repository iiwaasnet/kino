using System.Threading;

namespace kino.Connectivity
{
    public interface ILocalReceivingSocket<T>
    {
        T TryReceive();

        WaitHandle CanReceive();
    }
}