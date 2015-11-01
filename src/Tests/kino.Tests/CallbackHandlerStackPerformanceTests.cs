using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using C5;
using kino.Client;
using kino.Connectivity;
using kino.Diagnostics;
using kino.Framework;
using kino.Messaging;
using kino.Tests.Actors.Setup;
using Moq;
using NUnit.Framework;

namespace kino.Tests
{
    [TestFixture]
    public class CallbackHandlerStackPerformanceTests
    {
        [Test]
        public void Test()
        {
            var count = 100000;

            var logger = new Mock<ILogger>();
            var config = new ExpirableItemCollectionConfiguration
            {
                EvaluationInterval = TimeSpan.FromSeconds(10)
            };
            var callbackStack = new CallbackHandlerStack(new ExpirableItemScheduledCollection<CorrelationId>(config, logger.Object));
            var messageIdentifiers = new[]
                                     {
                                         MessageIdentifier.Create<SimpleMessage>(),
                                         MessageIdentifier.Create<AsyncMessage>()
                                     };
            var correlationIds = new List<CorrelationId>(count);
            for (var i = 0; i < count; i++)
            {
                correlationIds.Add(new CorrelationId(Guid.NewGuid().ToByteArray()));
            }

            var timer = new Stopwatch();
            timer.Start();
            for (var i = 0; i < count; i++)
            {
                callbackStack.Push(correlationIds[i], new Promise(), messageIdentifiers);
            }
            timer.Stop();

            timer.Reset();

            var messageIdentifier = messageIdentifiers.First();
            timer.Start();
            for (var i = 0; i < count; i++)
            {
                callbackStack.Pop(new CallbackHandlerKey
                {
                    Correlation = correlationIds[i].Value,
                    Version = messageIdentifier.Version,
                    Identity = messageIdentifier.Identity
                });
            }
            timer.Stop();
        }

        [Test]
        public void TestConcurrentDictionary()
        {
            var messageIdentifiers = new[]
                                     {
                                         MessageIdentifier.Create<SimpleMessage>(),
                                         MessageIdentifier.Create<AsyncMessage>()
                                     };
            var dictionary = new ConcurrentDictionary<CorrelationId, IEnumerable<MessageIdentifier>>();
            var timer = new Stopwatch();
            timer.Start();

            var count = 10000;
            for (var i = 0; i < count; i++)
            {
                dictionary.TryAdd(new CorrelationId(Guid.NewGuid().ToByteArray()), messageIdentifiers);
            }

            timer.Stop();
        }

        [Test]
        public void TestDictionary()
        {
            var messageIdentifiers = new[]
                                     {
                                         MessageIdentifier.Create<SimpleMessage>(),
                                         MessageIdentifier.Create<AsyncMessage>()
                                     };
            var dictionary = new Dictionary<CorrelationId, IEnumerable<MessageIdentifier>>();
            var timer = new Stopwatch();
            timer.Start();

            var count = 10000;
            for (var i = 0; i < count; i++)
            {
                dictionary.Add(new CorrelationId(Guid.NewGuid().ToByteArray()), messageIdentifiers);
            }

            timer.Stop();
        }

        [Test]
        public void TestHashDictionary()
        {
            var messageIdentifiers = new[]
                                     {
                                         MessageIdentifier.Create<SimpleMessage>(),
                                         MessageIdentifier.Create<AsyncMessage>()
                                     };
            var dictionary = new HashDictionary<CorrelationId, IEnumerable<MessageIdentifier>>();
            var timer = new Stopwatch();
            timer.Start();

            var count = 10000;
            for (var i = 0; i < count; i++)
            {
                dictionary.Add(new CorrelationId(Guid.NewGuid().ToByteArray()), messageIdentifiers);
            }

            timer.Stop();
        }

        [Test]
        public void TestHashCode()
        {
            var array = Guid.NewGuid().ToByteArray();
            var hashGenerator = SHA1.Create();

            var timer = new Stopwatch();
            timer.Start();

            var count = 10000;
            for (var i = 0; i < count; i++)
            {
                var hashCode = array.ComputeHash();
            }

            timer.Stop();
        }

        
    }
}