namespace kino.Core.Diagnostics.Performance
{
    public interface IPerformanceCounterManager<TCategory> where TCategory : struct
    {
        IPerformanceCounter GetCounter(TCategory counter);
    }
}