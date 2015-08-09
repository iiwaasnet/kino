using ProtoBuf;
using rawf.Framework;
using rawf.Messaging;

namespace ConsoleApplication1
{
    [ProtoContract]
    public class HelloMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "HELLO".GetBytes();

        [ProtoMember(1)]
        public string Greeting { get; set; }
    }
}