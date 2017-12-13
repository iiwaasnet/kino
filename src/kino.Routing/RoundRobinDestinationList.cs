using System;
using C5;
using kino.Core;

namespace kino.Routing
{
    public class RoundRobinDestinationList : IRoundRobinDestinationList
    {
        private readonly HashedLinkedList<IDestination> destinations;

        public RoundRobinDestinationList()
            => destinations = new HashedLinkedList<IDestination>();

        public IDestination SelectNextDestination(IDestination first, IDestination second)
        {
            var firstIndex = destinations.IndexOf(first);
            if (firstIndex < 0)
            {
                destinations.InsertLast(first);
                return first;
            }
            var secondIndex = destinations.IndexOf(second);
            if (secondIndex < 0)
            {
                destinations.InsertLast(second);
                return second;
            }
            if (firstIndex < secondIndex)
            {
                destinations.RemoveAt(firstIndex);
                destinations.InsertLast(first);

                return first;
            }

            destinations.RemoveAt(secondIndex);
            destinations.InsertLast(second);

            return second;
        }

        public void Add(IDestination destination)
        {
            if (!destinations.Add(destination))
            {
                throw new Exception($"Destination [{destination}] already exists!");
            }
        }

        public void Remove(IDestination destination)
            => destinations.Remove(destination);
    }
}