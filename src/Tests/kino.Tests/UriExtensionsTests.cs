using kino.Core.Framework;
using Xunit;

namespace kino.Tests
{
    
    public class UriExtensionsTests
    {
        [Fact]
        public void ToSocketAddress_ConvertsUriWithHostWildcard_ToFullUri()
        {
            "tcp://*:8000".ParseAddress();
        }
    }
}