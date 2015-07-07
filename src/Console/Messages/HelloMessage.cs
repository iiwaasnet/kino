using ProtoBuf;
using rawf.Framework;
using rawf.Messaging;

namespace Console.Messages
{
    [ProtoContract]
    public class HelloMessage : IPayload
    {
        public static readonly byte[] MessageIdentity = "HELLO".GetBytes();

        [ProtoMember(1)]
        public string Greeting { get; set; }
    }
}