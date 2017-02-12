using System.Diagnostics.CodeAnalysis;
using System.Security;

namespace kino.Security
{
    [ExcludeFromCodeCoverage]
    public class MessageNotSupportedException : SecurityException
    {
        public MessageNotSupportedException(string message) : base(message)
        {
        }
    }
}