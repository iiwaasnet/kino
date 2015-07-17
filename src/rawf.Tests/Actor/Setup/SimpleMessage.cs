using ProtoBuf;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Tests.Actor.Setup
{
    [ProtoContract]
    public class SimpleMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "SIMPLE".GetBytes();

        [ProtoMember(1)]
        public string Message { get; set; }
    }
}