using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using kino.Connectivity;
using kino.Core.Diagnostics.Performance;
using kino.Messaging;
using Moq;

namespace kino.Tests.Actors.Setup
{
    public class MockSocket : Mock<ISocket>, ISocket
    {
        private readonly BlockingCollection<IMessage> sentMessages;
        private readonly BlockingCollection<IMessage> receivedMessages;
        private bool connected;

        public MockSocket()
        {
            sentMessages = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
            receivedMessages = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
            connected = false;
            Setup(m => m.GetIdentity()).Returns((byte[]) null);
        }

        public void Send(IMessage message)
        {
            Setup(m => m.Send(message));
            Object.Send(message);
            sentMessages.Add(message);
        }

        public IMessage Receive(CancellationToken cancellationToken)
        {
            try
            {
                var message = receivedMessages.Take(cancellationToken);
                Setup(m => m.Receive(cancellationToken)).Returns(message);
                return Object.Receive(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public void Unbind(string address)
        {
            Object.Unbind(address);
        }

        public void Subscribe(string topic = "")
        {
            Object.Subscribe(topic);
        }

        public void Subscribe(byte[] topic)
        {
            Object.Subscribe(topic);
        }

        public void Unsubscribe(string topic = "")
        {
            Object.Unsubscribe(topic);
        }

        public void Connect(string address, bool waitUntilConnected = false)
        {
            Object.Connect(address);
            connected = true;
        }

        public void Disconnect(string address)
        {
            Object.Disconnect(address);
            connected = false;
        }

        public void Bind(string address)
        {
            Object.Bind(address);
        }

        public void SetMandatoryRouting(bool mandatory = true)
        {
            Object.SetMandatoryRouting(mandatory);
        }

        public void SetReceiveHighWaterMark(int hwm)
        {
            Object.SetReceiveHighWaterMark(hwm);
        }

        public void SetIdentity(byte[] identity)
        {
            Object.SetIdentity(identity);
            Setup(m => m.GetIdentity()).Returns(identity);
        }

        public byte[] GetIdentity()
            => Object.GetIdentity();

        public void Dispose()
            => Object.Dispose();

        internal IEnumerable<IMessage> GetSentMessages()
            => sentMessages;

        internal void DeliverMessage(IMessage message)
            => receivedMessages.Add(message);

        internal bool IsConnected()
            => connected;

        public IPerformanceCounter ReceiveRate { get; set; }

        public IPerformanceCounter SendRate { get; set; }
    }
}