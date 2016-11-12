using System;

namespace kino.Core.Diagnostics.Performance
{
    public interface IPerformanceCounterManager<TCategory> : IDisposable
        where TCategory : struct
    {
        IPerformanceCounter GetCounter(TCategory counter);
    }
}