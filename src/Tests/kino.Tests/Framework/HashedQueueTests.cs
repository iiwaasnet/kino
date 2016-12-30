using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core;
using kino.Core.Framework;
using kino.Tests.Helpers;
using NUnit.Framework;

namespace kino.Tests.Framework
{
    [TestFixture]
    public class HashedQueueTests
    {
        [Test]
        public void TryEnqueue_DoesntInsertDuplicatedItems()
        {
            var queue = new HashedQueue<MessageIdentifier>();

            var messageIdentifier = CreateMessageIdentifier();
            Assert.IsTrue(queue.TryEnqueue(messageIdentifier));
            Assert.IsFalse(queue.TryEnqueue(messageIdentifier));

            IList<MessageIdentifier> messageIdentifiers;
            queue.TryPeek(out messageIdentifiers, 10);
            Assert.AreEqual(1, messageIdentifiers.Count);
        }

        [Test]
        public void TryEnqueue_DoesntInsertItemsMoreThanMaxQueueLengthSize()
        {
            var maxQueueLength = 2;
            var queue = new HashedQueue<MessageIdentifier>(maxQueueLength);

            for (var i = 0; i < maxQueueLength + 2; i++)
            {
                if (i < maxQueueLength)
                {
                    Assert.IsTrue(queue.TryEnqueue(CreateMessageIdentifier()));
                }
                else
                {
                    Assert.IsFalse(queue.TryEnqueue(CreateMessageIdentifier()));
                }
            }

            IList<MessageIdentifier> messageIdentifiers;
            queue.TryPeek(out messageIdentifiers, maxQueueLength + 2);
            Assert.AreEqual(maxQueueLength, messageIdentifiers.Count);
        }

        [Test]
        public void TryEnqueue_AddsItemsAtEnd()
        {
            var queue = new HashedQueue<MessageIdentifier>();

            var ms1 = CreateMessageIdentifier();
            var ms2 = CreateMessageIdentifier();

            queue.TryEnqueue(ms1);
            queue.TryEnqueue(ms2);

            IList<MessageIdentifier> messageIdentifiers;
            queue.TryPeek(out messageIdentifiers, 2);

            Assert.AreEqual(ms1, messageIdentifiers.First());
            Assert.AreEqual(ms2, messageIdentifiers.Second());
        }

        [Test]
        public void TryDelete_DeletesOnlySpecificItems()
        {
            var queue = new HashedQueue<MessageIdentifier>();

            var ms1 = CreateMessageIdentifier();
            var ms2 = CreateMessageIdentifier();

            queue.TryEnqueue(ms1);
            queue.TryEnqueue(ms2);

            queue.TryDelete(new[] {ms2});

            IList<MessageIdentifier> messageIdentifiers;
            Assert.IsTrue(queue.TryPeek(out messageIdentifiers, 2));
            Assert.AreEqual(ms1, messageIdentifiers.First());

            queue.TryDelete(new[] {ms1});
            Assert.IsFalse(queue.TryPeek(out messageIdentifiers, 2));
        }

        [Test]
        public void TryDelete_DoesntFailIfDeletingMoreItemsThanExist()
        {
            var queue = new HashedQueue<MessageIdentifier>();

            var itemsCount = 6;
            var messageIdentifiers = Enumerable.Range(0, itemsCount)
                                               .Select(_ => CreateMessageIdentifier())
                                               .ToList();

            for (var i = 0; i < messageIdentifiers.Count - 2; i++)
            {
                queue.TryEnqueue(CreateMessageIdentifier());
            }

            queue.TryDelete(messageIdentifiers);
        }

        private static MessageIdentifier CreateMessageIdentifier()
        => new MessageIdentifier(Guid.NewGuid().ToByteArray(), Randomizer.UInt16(), Guid.NewGuid().ToByteArray());
        
    }
}