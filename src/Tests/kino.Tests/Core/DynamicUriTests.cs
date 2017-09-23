using System;
using kino.Core;
using kino.Core.Framework;
using NUnit.Framework;

namespace kino.Tests.Core
{
    
    public class DynamicUriTests
    {
        [Fact]
        public void DynamicUri_ResolvesLoopbackAddressToNICIpAddress()
        {
            var loopback = "tcp://127.0.0.1:80";
            var dynamicUri = new DynamicUri(loopback);
            //
            Assert.AreNotEqual(new Uri(loopback), dynamicUri.Uri);
            Assert.Equal(loopback.ParseAddress(), dynamicUri.Uri);
        }
    }
}