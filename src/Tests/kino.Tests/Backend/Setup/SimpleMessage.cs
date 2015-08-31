using kino.Framework;
using kino.Messaging;
using ProtoBuf;

namespace kino.Tests.Backend.Setup
{
    [ProtoContract]
    public class SimpleMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "SIMPLE".GetBytes();

        [ProtoMember(1)]
        public string Message { get; set; }
    }
}