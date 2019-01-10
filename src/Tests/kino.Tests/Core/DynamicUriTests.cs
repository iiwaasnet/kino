using System;
using kino.Core;
using kino.Core.Framework;
using NUnit.Framework;

namespace kino.Tests.Core
{
    public class DynamicUriTests
    {
        [Test]
        public void DynamicUri_ResolvesLoopbackAddressToNICIpAddress()
        {
            // arrange
            var loopback = "tcp://127.0.0.1:80";
            // act
            var dynamicUri = new DynamicUri(loopback);
            // assert
            Assert.AreNotEqual(new Uri(loopback), dynamicUri.Uri);
            Assert.AreEqual(loopback.ParseAddress(), dynamicUri.Uri);
        }

        [Test]
        public void DynamicUri_ResolvesWildcardAddressToNICIpAddress()
        {
            // arrange
            var wildcard = "tcp://*:0";
            // act
            var dynamic = new DynamicUri(wildcard);
            // assert
            Assert.AreEqual(wildcard.ParseAddress(), dynamic.Uri);
        }
    }
}