using System;
using System.Diagnostics;

namespace kino.Core.Diagnostics.Performance
{
    internal class SafePerformanceCounter : IPerformanceCounter, IDisposable
    {
        private readonly string categoryName;
        private readonly string name;
        private readonly string instanceName;
        private readonly ILogger logger;
        private PerformanceCounter perfCounter;

        public SafePerformanceCounter(string categoryName,
                                      string name,
                                      string instanceName,
                                      ILogger logger)
        {
            this.categoryName = categoryName;
            this.name = name;
            this.instanceName = instanceName;
            this.logger = logger;
            perfCounter = CreatePerfCounter();
        }

        private PerformanceCounter CreatePerfCounter()
        {
            try
            {
                return new PerformanceCounter(categoryName, name, instanceName, false);
            }
            catch (Exception err)
            {
                logger.Error(err);
                logger.Warn($"Performance counter {categoryName}.{name} will be unavailable!");

                return null;
            }
        }

        public long Increment(long value = 1L)
            => Invoke(c => c.IncrementBy(value));

        public long Decrement(long value = 1L)
            => Increment(-value);

        public void SetValue(long value)
            => Invoke(c => c.RawValue = value);

        public long GetRawValue()
            => Invoke(c => c.RawValue);

        public float NextValue()
            => Invoke(c => c.NextValue());

        public CounterSample NextSample()
            => Invoke(c => c.NextSample());

        private TResult Invoke<TResult>(Func<PerformanceCounter, TResult> func)
        {
            if (perfCounter != null)
            {
                try
                {
                    return func(perfCounter);
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                    logger.Warn($"Performance counter {categoryName}.{name} will be unavailable!");

                    perfCounter = null;
                }
            }

            return default(TResult);
        }

        void IDisposable.Dispose()
        {
            perfCounter?.RemoveInstance();
            perfCounter?.Dispose();
        }

        public bool IsEnabled() => perfCounter != null;
    }
}