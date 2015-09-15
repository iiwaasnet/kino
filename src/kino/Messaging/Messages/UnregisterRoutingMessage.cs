using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
	[ProtoContract]
	public class UnregisterRoutingMessage : Payload
	{
		public static readonly byte[] MessageIdentity = "UNREGROUTE".GetBytes();
		
		[ProtoMember(1)]
		public string Uri { get; set; }
		
		[ProtoMember(2)]
		public byte[] SocketIdentity { get; set; }
	}
}