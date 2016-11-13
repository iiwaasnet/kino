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

        public void SendMessage(IMessage message)
        {
            Setup(m => m.SendMessage(message));
            Object.SendMessage(message);
            sentMessages.Add(message);
        }

        public IMessage ReceiveMessage(CancellationToken cancellationToken)
        {
            try
            {
                var message = receivedMessages.Take(cancellationToken);
                Setup(m => m.ReceiveMessage(cancellationToken)).Returns(message);
                return Object.ReceiveMessage(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public void Unbind(Uri address)
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

        public void Connect(Uri address, bool waitConnectionEstablishment = false)
        {
            Object.Connect(address);
            connected = true;
        }

        public void Disconnect(Uri address)
        {
            Object.Disconnect(address);
            connected = false;
        }

        public void Bind(Uri address)
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