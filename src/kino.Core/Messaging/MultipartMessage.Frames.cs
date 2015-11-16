namespace kino.Core.Messaging
{
    internal partial class MultipartMessage
    {
        private class ReversedFrames
        {
            internal const int Body = 1;
            internal const int TTL = 3;
            internal const int CallbackReceiverIdentity = 4;
            internal const int CallbackStartFrame = 5;
            internal const int CallbackEntryCount = 6;
            internal const int CorrelationId = 7;
            internal const int DistributionPattern = 8;
            internal const int ReceiverIdentity = 9;
            internal const int Identity = 10;
            internal const int Version = 11;
            internal const int TraceOptions = 12;
            internal const int MessageRoutingStartFrame = 13;
            internal const int MessageRoutingEntryCount = 14;
        }

        private class ForwardFrames
        {
            internal const int SocketIdentity = 0;
        }
    }
}