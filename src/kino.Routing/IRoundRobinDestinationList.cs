using kino.Core;

namespace kino.Routing
{
    public interface IRoundRobinDestinationList
    {
        IDestination SelectNextDestination(IDestination first, IDestination second);

        void Add(IDestination destination);

        void Remove(IDestination destination);
    }
}