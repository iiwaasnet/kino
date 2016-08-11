using System;

namespace kino.Tests.Helpers
{
    public static class TimeSpanExtensions
    {
        public static TimeSpan DivideBy(this TimeSpan timeSpan, int divisor)
            => TimeSpan.FromMilliseconds(timeSpan.TotalMilliseconds / divisor);
    }
}