using System;

namespace rawf.Sockets
{
    public abstract class SocketStateChangeRequest
    {
        public SocketStateChangeKind StateChange { get; set; }
    }

    public class SocketConnectionStateChanged : SocketStateChangeRequest
    {
        public Uri Endpoint { get; set; }
    }

    public class SocketSubscriptionStateChanged : SocketStateChangeRequest
    {
        public string Topic { get; set; }
    }

    public enum SocketStateChangeKind
    {
        Connect,
        Disconnect,
        Subscribe,
        Unsubscribe
    }
}