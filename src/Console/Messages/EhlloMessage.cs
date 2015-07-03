using ProtoBuf;
using rawf.Messaging;

namespace Console.Messages
{
    [ProtoContract]
    public class EhlloMessage : IPayload
    {
        public const string MessageIdentity = "EHHLO";

        [ProtoMember(1)]
        public string Ehllo { get; set; }
    }
}