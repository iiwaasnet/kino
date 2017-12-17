using System;
using kino.Core;
using kino.Core.Diagnostics.Performance;

namespace kino.Connectivity
{
    public interface ILocalSendingSocket<T> : IEquatable<ILocalSendingSocket<T>>, IDestination
    {
        void Send(T message);

        IPerformanceCounter SendRate { get; set; }
    }
}