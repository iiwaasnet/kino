using System;

namespace kino.Messaging
{
    [Flags]
    public enum MessageTraceOptions : long
    {
        None = 0,
        Routing = 2
    }
}