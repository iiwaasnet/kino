using System;
using System.Collections.Generic;
using System.Linq;

namespace kino.Tests.Helpers
{
    public static class EnumerableExtenions
    {
        public static IEnumerable<T> Produce<T>(int times, Func<T> factory)
            => Enumerable.Range(0, times)
                         .Select(_ => factory())
                         .ToList();

        public static IEnumerable<T> Produce<T>(int times, Func<int, T> factory)
            => Enumerable.Range(0, times)
                         .Select(factory)
                         .ToList();
    }
}