using kino.Core;

namespace kino.Routing
{
    public class MessageContract
    {
        public MessageIdentifier Message { get; set; }

        public bool KeepRegistrationLocal { get; set; }
    }
}