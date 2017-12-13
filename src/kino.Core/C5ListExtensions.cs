using C5;

namespace kino.Core
{
    public static class C5ListExtensions
    {
        public static T RoundRobinGet<T>(this IList<T> hashSet)
        {
            var count = hashSet.Count;
            if (count > 0)
            {
                var first = (count > 1)
                                ? hashSet.RemoveFirst()
                                : hashSet.First;
                if (count > 1)
                {
                    hashSet.InsertLast(first);
                }

                return first;
            }

            return default;
        }
    }
}