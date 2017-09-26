using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core;
using kino.Core.Framework;
using kino.Tests.Helpers;
using Xunit;

namespace kino.Tests.Framework
{
    public class HashedQueueTests
    {
        [Fact]
        public void TryEnqueue_DoesntInsertDuplicatedItems()
        {
            var queue = new HashedQueue<MessageIdentifier>();

            var messageIdentifier = CreateMessageIdentifier();
            Assert.True(queue.TryEnqueue(messageIdentifier));
            Assert.False(queue.TryEnqueue(messageIdentifier));

            IList<MessageIdentifier> messageIdentifiers;
            queue.TryPeek(out messageIdentifiers, 10);
            Assert.Equal(1, messageIdentifiers.Count);
        }

        [Fact]
        public void TryEnqueue_DoesntInsertItemsMoreThanMaxQueueLengthSize()
        {
            var maxQueueLength = 2;
            var queue = new HashedQueue<MessageIdentifier>(maxQueueLength);

            for (var i = 0; i < maxQueueLength + 2; i++)
            {
                if (i < maxQueueLength)
                {
                    Assert.True(queue.TryEnqueue(CreateMessageIdentifier()));
                }
                else
                {
                    Assert.False(queue.TryEnqueue(CreateMessageIdentifier()));
                }
            }

            IList<MessageIdentifier> messageIdentifiers;
            queue.TryPeek(out messageIdentifiers, maxQueueLength + 2);
            Assert.Equal(maxQueueLength, messageIdentifiers.Count);
        }

        [Fact]
        public void TryEnqueue_AddsItemsAtEnd()
        {
            var queue = new HashedQueue<MessageIdentifier>();

            var ms1 = CreateMessageIdentifier();
            var ms2 = CreateMessageIdentifier();

            queue.TryEnqueue(ms1);
            queue.TryEnqueue(ms2);

            IList<MessageIdentifier> messageIdentifiers;
            queue.TryPeek(out messageIdentifiers, 2);

            Assert.Equal(ms1, messageIdentifiers.First());
            Assert.Equal(ms2, messageIdentifiers.Second());
        }

        [Fact]
        public void TryDelete_DeletesOnlySpecificItems()
        {
            var queue = new HashedQueue<MessageIdentifier>();

            var ms1 = CreateMessageIdentifier();
            var ms2 = CreateMessageIdentifier();

            queue.TryEnqueue(ms1);
            queue.TryEnqueue(ms2);

            queue.TryDelete(new[] {ms2});

            IList<MessageIdentifier> messageIdentifiers;
            Assert.True(queue.TryPeek(out messageIdentifiers, 2));
            Assert.Equal(ms1, messageIdentifiers.First());

            queue.TryDelete(new[] {ms1});
            Assert.False(queue.TryPeek(out messageIdentifiers, 2));
        }

        [Fact]
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