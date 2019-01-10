using System;
using kino.Core;
using kino.Core.Diagnostics.Performance;

namespace kino.Connectivity
{
    public interface ILocalSendingSocket<T> : ISendingSocket<T>, IEquatable<ILocalSendingSocket<T>>, IDestination
    {
        IPerformanceCounter SendRate { get; set; }
    }
}