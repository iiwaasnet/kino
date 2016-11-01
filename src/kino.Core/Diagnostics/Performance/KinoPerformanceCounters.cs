using System.Diagnostics;

namespace kino.Core.Diagnostics.Performance
{
    [PerformanceCounterCategory("kino", PerformanceCounterCategoryType.MultiInstance)]
    public enum KinoPerformanceCounters
    {
        [PerformanceCounterDefinition("ClusterListener Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] ClusterListenerSocketReceiveRate,

        [PerformanceCounterDefinition("ClusterListener Internal Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] ClusterListenerInternalSocketSendRate,

        [PerformanceCounterDefinition("ClusterSender Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] ClusterSenderSocketSendRate,

        [PerformanceCounterDefinition("MessageHub Request Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] MessageHubRequestSocketSendRate,

        [PerformanceCounterDefinition("MessageHub Response Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] MessageHubResponseSocketReceiveRate,

        [PerformanceCounterDefinition("ActorHost Async Response Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] ActorHostAsyncResponseSocketSendRate,

        [PerformanceCounterDefinition("ActorHost Registration Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] ActorHostRegistrationSocketSendRate,

        [PerformanceCounterDefinition("ActorHost Request Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] ActorHostRequestSocketReceiveRate,

        [PerformanceCounterDefinition("ActorHost Request Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] ActorHostRequestSocketSendRate,

        [PerformanceCounterDefinition("Rendezvous HeartBeat Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)]
        RendezvousHeartBeatSocketSendRate,

        [PerformanceCounterDefinition("Rendezvous Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] RendezvousSocketReceiveRate,

        [PerformanceCounterDefinition("Rendezvous Broadcast Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] RendezvousBroadcastSocketSendRate,

        [PerformanceCounterDefinition("MessageRouter Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] MessageRouterSocketReceiveRate,

        [PerformanceCounterDefinition("MessageRouter Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] MessageRouterSocketSendRate,

        [PerformanceCounterDefinition("MessageRouter Scaleout Frontend Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] MessageRouterScaleoutFrontendSocketSendRate,

        [PerformanceCounterDefinition("MessageRouter Scaleout Frontend Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] MessageRouterScaleoutFrontendSocketReceiveRate,

        [PerformanceCounterDefinition("MessageRouter Scaleout Backend Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] MessageRouterScaleoutBackendSocketSendRate,

        [PerformanceCounterDefinition("Intercom Multicast Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] IntercomMulticastSocketReceiveRate,

        [PerformanceCounterDefinition("Intercom Unicast Socket Receive Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] IntercomUnicastSocketReceiveRate,

        [PerformanceCounterDefinition("Intercom Socket Send Rate(/sec)", PerformanceCounterType.RateOfCountsPerSecond64)] IntercomSocketSendRate,
    }
}