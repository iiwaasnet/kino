using System;

namespace rawf.Sockets
{
	public class SocketStateChangeRequest
	{
		public Uri Endpoint {get; set; }
		public SocketStateChangeKind StateChange { get; set; }
	}
	
	public enum SocketStateChangeKind
	{
		Connect,
		Disconnect
	}
}