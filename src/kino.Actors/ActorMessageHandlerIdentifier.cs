using kino.Core;

namespace kino.Actors
{
    public class ActorMessageHandlerIdentifier
    {
        public MessageIdentifier Identifier { get; set; }

        public bool KeepRegistrationLocal { get; set; }
    }
}