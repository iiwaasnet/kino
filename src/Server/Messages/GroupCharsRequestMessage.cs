using ProtoBuf;
using rawf.Framework;
using rawf.Messaging;

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