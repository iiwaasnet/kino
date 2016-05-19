namespace kino.Core.Messaging
{
    internal partial class MultipartMessage
    {
        private class ReversedFramesV1
        {
            internal const int Body = 1;
            internal const int WireFormatVersion = 3;
            internal const int TTL = 4;
            internal const int CallbackReceiverIdentity = 5;
            internal const int CallbackStartFrame = 6;
            internal const int CallbackEntryCount = 7;
            internal const int CorrelationId = 8;
            internal const int DistributionPattern = 9;
            internal const int ReceiverIdentity = 10;
            internal const int Identity = 11;
            internal const int Version = 12;
            internal const int TraceOptions = 13;
            internal const int MessageRoutingStartFrame = 14;
            internal const int MessageRoutingEntryCount = 15;
            internal const int MessageHops = 16;
        }

        private class ReversedFramesV2
        {
            internal const int ReceiverNodeIdentity = 17;
        }

        private class ReversedFramesV3
        {
            internal const int Partition = 18;
            internal const int RoutingFrameDivisor = 19;
            internal const int CallbackFrameDivisor = 20;
        }

        internal static int GetLastFixedFrameIndex()
            => ReversedFramesV3.CallbackFrameDivisor;

        private class ForwardFrames
        {
            internal const int SocketIdentity = 0;
        }
    }
}