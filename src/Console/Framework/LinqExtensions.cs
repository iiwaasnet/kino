﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Console.Framework
{
    public static class LinqExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> collection, Action<T> exp)
        {
            foreach (var el in collection)
            {
                exp(el);
            }
        }

        public static T Second<T>(this IEnumerable<T> collection)
        {
            return collection.Skip(1).First();
        }

        public static T Third<T>(this IEnumerable<T> collection)
        {
            return collection.Skip(2).First();
        }
    }
}