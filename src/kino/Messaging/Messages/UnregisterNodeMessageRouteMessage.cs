using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
	[ProtoContract]
	public class UnregisterNodeMessageRouteMessage : Payload
	{
		public static readonly byte[] MessageIdentity = "UNREGNODEROUTE".GetBytes();
		
		[ProtoMember(1)]
		public string Uri { get; set; }
		
		[ProtoMember(2)]
		public byte[] SocketIdentity { get; set; }

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}