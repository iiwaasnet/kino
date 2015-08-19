using System.Collections.Generic;
using ProtoBuf;
using rawf.Framework;
using rawf.Messaging;

namespace Client.Messages
{
  [ProtoContract]
  public class GroupCharsResponseMessage : Payload
  {
    public static readonly byte[] MessageIdentity = "GRPCHARSRESP".GetBytes();

    [ProtoMember(1)]
    public IEnumerable<GroupInfo> Groups { get; set; }

    [ProtoMember(2)]
    public string Text { get; set; }
  }

  [ProtoContract]
  public class GroupInfo
  {
    [ProtoMember(1)]
    public char Char { get; set; }

    [ProtoMember(2)]
    public int Count { get; set; }
  }
}