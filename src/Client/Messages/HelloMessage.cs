using kino.Framework;
using kino.Messaging;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class HelloMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "HELLO".GetBytes();

        [ProtoMember(1)]
        public string Greeting { get; set; }
    }
}