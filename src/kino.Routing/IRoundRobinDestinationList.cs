using kino.Core;

namespace kino.Routing
{
    public interface IRoundRobinDestinationList
    {
        IDestination SelectNextDestination(params IDestination[] receivers);

        void Add(IDestination destination);

        void Remove(IDestination destination);
    }
}