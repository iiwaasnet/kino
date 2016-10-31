using System.Threading;

namespace kino.Core.Connectivity
{
    public interface ILocalReceivingSocket<T>
    {
        T TryReceive();

        WaitHandle CanReceive();        
    }
}