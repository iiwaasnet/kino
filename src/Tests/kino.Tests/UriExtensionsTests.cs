using kino.Core.Framework;
using NUnit.Framework;

namespace kino.Tests
{
    [TestFixture]
    public class UriExtensionsTests
    {
        [Test]
        public void ToSocketAddress_ConvertsUriWithHostWildcard_ToFullUri()
        {
            Assert.DoesNotThrow(() => "tcp://*:8000".ParseAddress());
        }
    }
}