using System;

namespace kino.Core.Messaging
{
    [Flags]
    public enum MessageTraceOptions : ushort
    {
        None = 0,
        Routing = 1 << 0
    }
}