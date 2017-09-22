#if NET47
using System.Diagnostics;
#endif

namespace kino.Core.Diagnostics.Performance
{
    public interface IPerformanceCounter
    {
        long Increment(long value = 1);

        long Decrement(long value = 1);

        void SetValue(long value);

        long GetRawValue();

        float NextValue();
#if NET47
        CounterSample NextSample();
#endif
        bool IsEnabled();
    }
}