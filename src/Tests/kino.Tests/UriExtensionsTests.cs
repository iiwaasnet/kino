using kino.Core.Framework;
using NUnit.Framework;

namespace kino.Tests
{
    
    public class UriExtensionsTests
    {
        [Test]
        public void ToSocketAddress_ConvertsUriWithHostWildcard_ToFullUri()
        {
            "tcp://*:8000".ParseAddress();
        }
    }
}