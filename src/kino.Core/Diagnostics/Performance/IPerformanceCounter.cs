using System.Diagnostics;

namespace kino.Core.Diagnostics.Performance
{
    public interface IPerformanceCounter
    {
        long Increment(long value = 1);

        long Decrement(long value = 1);

        void SetValue(long value);

        long GetRawValue();

        float NextValue();

        CounterSample NextSample();

        bool IsEnabled();
    }
}