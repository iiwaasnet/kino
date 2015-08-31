using kino.Framework;
using kino.Messaging;
using ProtoBuf;

namespace Server.Messages
{
  [ProtoContract]
  public class GroupCharsRequestMessage : Payload
  {
    public static readonly byte[] MessageIdentity = "GRPCHARSREQ".GetBytes();

    [ProtoMember(1)]
    public string Text { get; set; }
  }
}