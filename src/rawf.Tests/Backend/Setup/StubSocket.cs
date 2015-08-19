using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using rawf.Messaging;
using rawf.Sockets;

namespace rawf.Tests.Backend.Setup
{
    public class StubSocket : ISocket
    {
        private byte[] identity;
        private readonly BlockingCollection<IMessage> sentMessages;
        private readonly BlockingCollection<IMessage> receivedMessages;

        public StubSocket()
        {
            sentMessages = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
            receivedMessages = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
        }

        public void SendMessage(IMessage message)
        {
            sentMessages.Add(message);
        }

        public IMessage ReceiveMessage(CancellationToken cancellationToken)
        {
            try
            {
                var message = receivedMessages.Take(cancellationToken);

                return message;
            }
            catch (TaskCanceledException)
            {
                return null;
            }
        }

        public void Unbind(Uri address)
        {
        }

        public void Subscribe(string topic = "")
        {
        }

        public void Subscribe(byte[] topic)
        {
        }

        public void Unsubscribe(string topic = "")
        {
        }

        public void Connect(Uri address)
        {
        }

        public void Disconnect(Uri address)
        {
        }

        public void Bind(Uri address)
        {
        }

        public void SetMandatoryRouting(bool mandatory = true)
        {
        }

        public void SetIdentity(byte[] identity)
            => this.identity = identity;

        public byte[] GetIdentity()
            => identity;

        public void Dispose()
        {
            receivedMessages.Dispose();
        }

        internal IEnumerable<IMessage> GetSentMessages()
            => sentMessages.GetConsumingEnumerable();

        internal void DeliverMessage(IMessage message)
            => receivedMessages.Add(message);
    }
}