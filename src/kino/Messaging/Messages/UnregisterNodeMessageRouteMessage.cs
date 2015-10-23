using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
	[ProtoContract]
	public class UnregisterNodeMessageRouteMessage : Payload
	{
		private static readonly byte[] MessageIdentity = "UNREGNODEROUTE".GetBytes();
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
		public string Uri { get; set; }
		
		[ProtoMember(2)]
		public byte[] SocketIdentity { get; set; }

        public override byte[] Version => MessageVersion;
        public override byte[] Identity => MessageIdentity;
    }
}