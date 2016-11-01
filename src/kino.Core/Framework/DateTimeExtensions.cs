using System;

namespace kino.Core.Framework
{
    public static class DateTimeExtensions
    {
        public static TimeSpan MultiplyBy(this TimeSpan timeSpan, int multiplier)
            => TimeSpan.FromTicks(timeSpan.Ticks * multiplier);
    }
}