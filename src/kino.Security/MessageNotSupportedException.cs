using System.Security;

namespace kino.Security
{
    public class MessageNotSupportedException : SecurityException
    {
        public MessageNotSupportedException(string message) : base(message)
        {
        }
    }
}