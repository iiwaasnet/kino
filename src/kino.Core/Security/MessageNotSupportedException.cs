using System.Security;

namespace kino.Core.Security
{
    public class MessageNotSupportedException : SecurityException
    {
        public MessageNotSupportedException(string message) : base(message)
        {
        }
    }
}