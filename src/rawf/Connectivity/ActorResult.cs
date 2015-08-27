using System.Collections.Generic;
using rawf.Messaging;

namespace rawf.Connectivity
{
    public class ActorResult : IActorResult
    {
        public ActorResult(IMessage[] messages)
        {
            Messages = messages;
        }

        public ActorResult(IMessage result)
            : this(new[] {result})
        {
        }

        public IEnumerable<IMessage> Messages { get; }
    }
}