using System;
using kino.Messaging;

namespace kino.Connectivity.Kafka
{
    public static class MessageTraceOptionsExtensions
    {
        public static MessageTraceOptions ToTraceOptions(this short traceOptions)
        {
            switch (traceOptions)
            {
                case 1:
                    return MessageTraceOptions.None;
                case 2:
                    return MessageTraceOptions.Routing;
            }

            throw new NotSupportedException(traceOptions.ToString());
        }

        public static short ToTraceOptionsCode(this MessageTraceOptions traceOptions)
        {
            switch (traceOptions)
            {
                case MessageTraceOptions.None:
                    return 1;
                case MessageTraceOptions.Routing:
                    return 2;
            }

            throw new NotSupportedException(traceOptions.ToString());
        }
    }
}