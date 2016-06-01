namespace kino.Core.Diagnostics.Performance
{
    public interface IPerformanceCounter
    {
        void Increment(uint value = 1);
        void Decrement(uint value = 1);
    }
}