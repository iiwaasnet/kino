using System;

namespace kino.Messaging
{
    [Flags]
    public enum MessageTraceOptions : ushort
    {
        None = 0,
        Routing = 1 << 0
    }
}