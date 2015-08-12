using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ConsoleApplication1
{
    internal class SortedCollection
    {
        private static void Main(string[] args)
        {
            var runs = 10000;

            RunListTest(runs);
            RunSortedList(runs);

            Console.ReadLine();
        }

        private static void RunListTest(int runs)
        {
            var list = new List<string>();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (var i = 0; i < runs; i++)
            {
                list.Add(Guid.NewGuid().ToString());

                list.Sort(Comparison);
            }

            stopwatch.Stop();

            Console.WriteLine($"List done {list.Count} in {stopwatch.ElapsedMilliseconds} msec");
        }

        private static void RunSortedList(int runs)
        {
            var list = new SortedSet<string>();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (var i = 0; i < runs; i++)
            {
                list.Add(Guid.NewGuid().ToString());
            }

            stopwatch.Stop();

            Console.WriteLine($"SortedList done {list.Count} in {stopwatch.ElapsedMilliseconds} msec");
        }

        private static int Comparison(string l, string l1)
        {
            return l.CompareTo(l);
        }
    }
}