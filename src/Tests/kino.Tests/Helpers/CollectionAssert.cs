using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace kino.Tests.Helpers
{
    public static class CollectionAssert
    {
        public static void AreEquivalent<T>(IEnumerable<T> expected, IEnumerable<T> actual)
            => Assert.Equal(expected.OrderBy(_ => _), actual.OrderBy(_ => _));
    }
}