namespace kino.Messaging
{
    internal partial class MultipartMessage
    {
        private class ReversedFramesV4
        {
            internal const int WireFormatVersion = 1;
            internal const int BodyDescription = 2;
            internal const int TTL = 3;
            internal const int CorrelationId = 4;
            internal const int TraceOptionsDistributiomPattern = 5;
            internal const int Identity = 6;
            internal const int Version = 7;
            internal const int Partition = 8;
            internal const int ReceiverNodeIdentity = 9;
            internal const int CallbackReceiverIdentity = 10;
            internal const int ReceiverIdentity = 11;
            internal const int CallbackDescription = 12;
            internal const int RoutingDescription = 13;
            internal const int Signature = 14;
            internal const int Domain = 15;
            internal const int CallbackKey = 16;
        }

        internal static ushort GetLastFixedFrameIndex()
            => ReversedFramesV4.CallbackKey;

        private class ForwardFrames
        {
            internal const int SocketIdentity = 0;
        }
    }
}