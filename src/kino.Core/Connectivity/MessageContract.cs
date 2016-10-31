using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public class MessageContract
    {
        public Identifier Identifier { get; set; }

        public bool KeepRegistrationLocal { get; set; }
    }
}