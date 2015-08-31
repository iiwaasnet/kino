using kino.Framework;
using kino.Messaging;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class EhlloMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "EHHLO".GetBytes();

        [ProtoMember(1)]
        public string Ehllo { get; set; }
    }
}