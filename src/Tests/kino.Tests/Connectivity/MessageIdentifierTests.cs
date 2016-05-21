using System;
using kino.Core.Connectivity;
using kino.Core.Framework;
using kino.Tests.Actors.Setup;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class MessageIdentifierTests
    {
        [Test]
        public void MessageIdentifier_NeverHasNullPartition()
        {
            var identifer = new MessageIdentifier(null, null, null);
            Assert.IsNotNull(identifer.Partition);
            Assert.IsTrue(Unsafe.Equals(IdentityExtensions.Empty, identifer.Partition));

            identifer = new MessageIdentifier((byte[]) null);
            Assert.IsNotNull(identifer.Partition);
            Assert.IsTrue(Unsafe.Equals(IdentityExtensions.Empty, identifer.Partition));

            identifer = new MessageIdentifier(new LocalMessage {Partition = null});
            Assert.IsNotNull(identifer.Partition);
            Assert.IsTrue(Unsafe.Equals(IdentityExtensions.Empty, identifer.Partition));
        }

        [Test]
        public void MessageIdentifiers_AreComparedByIdentityVersionPartition()
        {
            var identity = Guid.NewGuid().ToByteArray();
            var version = Guid.NewGuid().ToByteArray();
            var partition = Guid.NewGuid().ToByteArray();

            Assert.AreEqual(new MessageIdentifier(version, identity, partition),
                            new MessageIdentifier(version, identity, partition));

            Assert.AreEqual(new MessageIdentifier(version, identity, null),
                               new MessageIdentifier(version, identity, null));

            Assert.AreNotEqual(new MessageIdentifier(version, identity, null),
                               new MessageIdentifier(version, identity, partition));

            Assert.AreNotEqual(new MessageIdentifier(version, null, partition),
                               new MessageIdentifier(version, identity, partition));

            Assert.AreNotEqual(new MessageIdentifier(null, identity, partition),
                               new MessageIdentifier(version, identity, partition));
        }
    }
}