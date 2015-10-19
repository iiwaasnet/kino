using System;
using kino.Connectivity;
using kino.Messaging;
using kino.Tests.Actors.Setup;
using NUnit.Framework;

namespace kino.Tests
{
  [TestFixture]
  public class MessageHandlerIdentifierTests
  {
    [Test]
    public void TestTwoMessageHandlerIdentifiers_AreComparedByVersionIdentity()
    {
      var firstIdentifier = MessageIdentifier.Create<SimpleMessage>();
      var secondIdentifier = MessageIdentifier.Create<SimpleMessage>();

            Assert.AreEqual(firstIdentifier, secondIdentifier);
      Assert.IsTrue(firstIdentifier.Equals((object) secondIdentifier));

      var thirdIdentifier = new MessageIdentifier(Guid.NewGuid().ToByteArray(), firstIdentifier.Identity);

      Assert.AreNotEqual(firstIdentifier, thirdIdentifier);
      Assert.IsFalse(firstIdentifier.Equals((object) thirdIdentifier));
    }
  }
}