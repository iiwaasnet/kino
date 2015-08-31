using System;

namespace kino.Framework
{
    public static class TimeSpanExtensions
    {
        public static TimeSpan DivideBy(this TimeSpan timeSpan, int divider)
        {
            return TimeSpan.FromTicks(timeSpan.Ticks / divider);
        }
    }
}