using System;
using System.Diagnostics;

namespace kino.Core.Diagnostics.Performance
{
    internal class SafePerformanceCounter : IPerformanceCounter
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

        public void Increment(uint value = 1)
            => Invoke(value);

        public void Decrement(uint value = 1)
            => Invoke(-value);

        private void Invoke(long value)
        {
            try
            {
                perfCounter?.IncrementBy(value);
            }
            catch (Exception err)
            {
                logger.Error(err);
                logger.Warn($"Performance counter {categoryName}.{name} will be unavailable!");

                perfCounter = null;
            }
        }
    }
}