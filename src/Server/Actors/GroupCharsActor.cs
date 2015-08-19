using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using rawf.Actors;
using rawf.Connectivity;
using rawf.Messaging;
using Server.Messages;

namespace Server.Actors
{
  public class GroupCharsActor : IActor
  {
    public IEnumerable<MessageMap> GetInterfaceDefinition()
    {
      yield return new MessageMap
      {
        Handler = StartProcess,
        Message = new MessageDefinition
        {
          Identity = EhlloMessage.MessageIdentity,
          Version = Message.CurrentVersion
        }
      };
    }

    private async Task<IMessage> StartProcess(IMessage message)
    {
      var ehllo = message.GetPayload<EhlloMessage>();


      return Message.Create(new GroupCharsResponseMessage
      {
        Groups = ehllo.Ehllo.GroupBy(c => c).Select(g => new GroupInfo {Char = g.Key, Count = g.Count()}),
        Text = ehllo.Ehllo
      },
        GroupCharsResponseMessage.MessageIdentity);
    }
  }
}