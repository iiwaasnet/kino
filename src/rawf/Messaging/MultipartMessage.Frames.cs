using System.Collections.Generic;
using rawf.Connectivity;

namespace rawf.Messaging
{
    internal partial class MultipartMessage
    {
        private class ReversedFrames
        {
            internal const int Body = 1;
            internal const int TTL = 3;
            internal const int CallbackReceiverIdentity = 4;
            internal const int CallbackIdentity = 5;
            internal const int CorrelationId = 6;
            internal const int DistributionPattern = 7;
            internal const int ReceiverIdentity = 8;
            internal const int Identity = 9;
            internal const int Version = 10;
            internal const int NextRouterInsertPosition = 11;
        }

        private class ForwardFrames
        {
            internal const int SocketIdentity = 0;
        }        
    }
}