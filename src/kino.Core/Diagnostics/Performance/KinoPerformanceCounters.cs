#if NET47
using System.Diagnostics;
#endif

namespace kino.Core.Diagnostics.Performance
{
#if NET47
    [PerformanceCounterCategory("kino", PerformanceCounterCategoryType.MultiInstance)]
#endif
    public enum KinoPerformanceCounters
    {
#if NET47
        [PerformanceCounterDefinition("AutoDiscoveryListener Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)]
#endif
        AutoDiscoveryListenerSocketReceiveRate,

#if NET47
        [PerformanceCounterDefinition("AutoDiscoverySender Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)]
#endif
        AutoDiscoverySenderSocketSendRate,

#if NET47
        [PerformanceCounterDefinition("Rendezvous HeartBeat Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)]
#endif
        RendezvousHeartBeatSocketSendRate,

#if NET47
        [PerformanceCounterDefinition("Rendezvous Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)]
#endif
        RendezvousSocketReceiveRate,

#if NET47
        [PerformanceCounterDefinition("Rendezvous Broadcast Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)]
#endif
        RendezvousBroadcastSocketSendRate,

#if NET47
        [PerformanceCounterDefinition("MessageRouter Scaleout Frontend Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)]
#endif
        MessageRouterScaleoutFrontendSocketReceiveRate,

#if NET47
        [PerformanceCounterDefinition("MessageRouter Scaleout Backend Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)]
#endif
        MessageRouterScaleoutBackendSocketSendRate,

#if NET47
        [PerformanceCounterDefinition("MessageRouter Local Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)]
#endif
        MessageRouterLocalSocketReceiveRate,

#if NET47
        [PerformanceCounterDefinition("MessageRouter Local Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)]
#endif
        MessageRouterLocalSocketSendRate,

#if NET47
        [PerformanceCounterDefinition("Intercom Multicast Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)]
#endif
        IntercomMulticastSocketReceiveRate,

#if NET47
        [PerformanceCounterDefinition("Intercom Unicast Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)]
#endif
        IntercomUnicastSocketReceiveRate,

#if NET47
        [PerformanceCounterDefinition("Intercom Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)]
#endif
        IntercomSocketSendRate
    }
}