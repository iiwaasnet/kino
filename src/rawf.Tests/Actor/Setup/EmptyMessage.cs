using ProtoBuf;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Tests.Actor.Setup
{
    [ProtoContract]
    public class EmptyMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "EMPTY".GetBytes();
    }
}