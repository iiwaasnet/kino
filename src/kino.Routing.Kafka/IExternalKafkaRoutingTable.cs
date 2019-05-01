using System.Collections.Generic;
using kino.Core;

namespace kino.Routing.Kafka
{
    public interface IExternalKafkaRoutingTable
    {
        KafkaPeerConnection AddMessageRoute(ExternalKafkaRouteRegistration routeRegistration);

        IEnumerable<KafkaPeerConnection> FindRoutes(ExternalRouteLookupRequest lookupRequest);

        KafkaAppClusterRemoveResult RemoveNodeRoute(ReceiverIdentifier nodeIdentifier);

        KafkaAppClusterRemoveResult RemoveMessageRoute(ExternalRouteRemoval routeRemoval);

        IEnumerable<NodeActors> FindAllActors(MessageIdentifier messageIdentifier);

        IEnumerable<ExternalKafkaRoute> GetAllRoutes();
    }
}