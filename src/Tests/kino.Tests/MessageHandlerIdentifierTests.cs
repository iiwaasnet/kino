using System;
using kino.Core.Connectivity;
using kino.Core.Framework;
using kino.Tests.Actors.Setup;
using NUnit.Framework;

namespace kino.Tests
{
  [TestFixture]
  public class MessageHandlerIdentifierTests
  {
    [Test]
    public void TwoMessageHandlerIdentifiers_AreComparedByVersionIdentity()
    {
      var firstIdentifier = MessageIdentifier.Create<SimpleMessage>();
      var secondIdentifier = MessageIdentifier.Create<SimpleMessage>();

            Assert.AreEqual(firstIdentifier, secondIdentifier);
      Assert.IsTrue(firstIdentifier.Equals((object) secondIdentifier));

      var thirdIdentifier = new MessageIdentifier(Guid.NewGuid().ToByteArray(), firstIdentifier.Identity, IdentityExtensions.Empty);

      Assert.AreNotEqual(firstIdentifier, thirdIdentifier);
      Assert.IsFalse(firstIdentifier.Equals((object) thirdIdentifier));
    }
  }
}