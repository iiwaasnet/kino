using System;

namespace kino.Core.Messaging
{
    [Flags]
    public enum MessageTraceOptions : long
    {
        None = 0,
        Routing = 2
    }
}