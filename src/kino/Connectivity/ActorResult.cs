using System.Collections.Generic;
using kino.Messaging;

namespace kino.Connectivity
{
    public class ActorResult : IActorResult
    {
        public ActorResult(params IMessage[] messages)
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