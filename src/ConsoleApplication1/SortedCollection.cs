using System;
using System.Collections.Generic;
using System.Diagnostics;
using C5;

namespace ConsoleApplication1
{
    internal class SortedCollection
    {
        private static void Main(string[] args)
        {
            var runs = 10000;

            RunListTest(runs);
            RunSortedList(runs);
            RunPriorityQueue(runs);
            RunSortedArray(runs);

            Console.ReadLine();
        }

        private static void RunPriorityQueue(int runs)
        {
            var list = new IntervalHeap<string>();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

          var str = Guid.NewGuid().ToString();
            for (var i = 0; i < runs; i++)
            {
                list.Add(str);
                list.FindMin();
            }

            stopwatch.Stop();

            Console.WriteLine($"PriorityQueue done {list.Count} in {stopwatch.ElapsedMilliseconds} msec");
        }

        private static void RunSortedArray(int runs)
        {
            var list = new SortedArray<string>(Comparer<string>.Create(Comparison));

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (var i = 0; i < runs; i++)
            {
                list.Add(Guid.NewGuid().ToString());
            }

            stopwatch.Stop();

            Console.WriteLine($"SortedArray done {list.Count} in {stopwatch.ElapsedMilliseconds} msec");
        }

        private static void RunListTest(int runs)
        {
            var list = new List<string>();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (var i = 0; i < runs; i++)
            {
                list.Add(Guid.NewGuid().ToString());
                if (i % 100 == 0)
                {
                    list.Sort(Comparison);
                }
            }
            list.Sort(Comparison);

            stopwatch.Stop();

            Console.WriteLine($"List done {list.Count} in {stopwatch.ElapsedMilliseconds} msec");
        }

        private static void RunSortedList(int runs)
        {
            var list = new SortedSet<string>(Comparer<string>.Create(Comparison));

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (var i = 0; i < runs; i++)
            {
                list.Add(Guid.NewGuid().ToString());
                var el = list.Min;
            }

            stopwatch.Stop();

            Console.WriteLine($"SortedList done {list.Count} in {stopwatch.ElapsedMilliseconds} msec");
        }

        private static int Comparison(string l, string l1)
        {
            var res = l.CompareTo(l);
            return res == 0 ? 1 : res;
        }
    }
}