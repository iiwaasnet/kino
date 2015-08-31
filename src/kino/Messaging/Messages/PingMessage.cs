using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class PingMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "PING".GetBytes();
    }
}