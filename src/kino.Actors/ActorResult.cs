using System.Collections.Generic;
using System.Threading.Tasks;
using kino.Messaging;

namespace kino.Actors
{
    public class ActorResult : IActorResult
    {
        public static readonly IActorResult Empty = null;
        public static readonly Task<IActorResult> NoWait = Task.FromResult(Empty);

        public ActorResult(params IMessage[] messages)
            => Messages = messages;

        public ActorResult(IMessage result)
            : this(new[] {result})
        {
        }

        public IEnumerable<IMessage> Messages { get; }
    }
}