using System.Diagnostics;

namespace kino.Core.Diagnostics.Performance
{
    [PerformanceCounterCategory("kino", PerformanceCounterCategoryType.MultiInstance)]
    public enum KinoPerformanceCounters
    {
        [PerformanceCounterDefinition("AutoDiscoveryListener Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] AutoDiscoveryListenerSocketReceiveRate,

        [PerformanceCounterDefinition("AutoDiscoverySender Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] AutoDiscoverySenderSocketSendRate,

        [PerformanceCounterDefinition("Rendezvous HeartBeat Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] RendezvousHeartBeatSocketSendRate,

        [PerformanceCounterDefinition("Rendezvous Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] RendezvousSocketReceiveRate,

        [PerformanceCounterDefinition("Rendezvous Broadcast Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] RendezvousBroadcastSocketSendRate,

        [PerformanceCounterDefinition("MessageRouter Scaleout Frontend Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] MessageRouterScaleoutFrontendSocketReceiveRate,

        [PerformanceCounterDefinition("MessageRouter Scaleout Backend Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] MessageRouterScaleoutBackendSocketSendRate,

        [PerformanceCounterDefinition("Intercom Multicast Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] IntercomMulticastSocketReceiveRate,

        [PerformanceCounterDefinition("Intercom Unicast Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] IntercomUnicastSocketReceiveRate,

        [PerformanceCounterDefinition("Intercom Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] IntercomSocketSendRate,
    }
}