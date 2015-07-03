using ProtoBuf;
using rawf.Messaging;

namespace Console.Messages
{
    [ProtoContract]
    public class HelloMessage : IPayload
    {
        public const string MessageIdentity = "HELLO";

        [ProtoMember(1)]
        public string Greeting { get; set; }
    }
}