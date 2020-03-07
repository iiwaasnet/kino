using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace kino.Core.Framework
{
    public static class LinqExtensions
    {
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> collection, Action<T> action)
        {
            if (collection is ICollection<T>)
            {
                collection.ExecuteForEach(action);
            }
            else
            {
                collection = collection.Select(e =>
                                               {
                                                   action(e);
                                                   return e;
                                               })
                                       .ToList();
            }

            return collection;
        }

        public static void ExecuteForEach<T>(this IEnumerable<T> collection, Action<T> action)
        {
            foreach (var el in collection)
            {
                action(el);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Second<T>(this IEnumerable<T> collection)
            => collection.Skip(1).First();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Third<T>(this IEnumerable<T> collection)
            => collection.Skip(2).First();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> ToEnumerable<T>(this T single)
            => new[] {single};
    }
}