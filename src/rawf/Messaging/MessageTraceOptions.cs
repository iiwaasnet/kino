using System;

namespace rawf.Messaging
{
    [Flags]
    public enum MessageTraceOptions : long
    {
        None = 0,
        Routing = 2
    }
}