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
        private readonly Queue<IMessage> sentMessages;
        private readonly BlockingCollection<IMessage> receivedMessages;

        public StubSocket()
        {
            sentMessages = new Queue<IMessage>();
            receivedMessages = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
        }

        public void SendMessage(IMessage message)
        {
            sentMessages.Enqueue(message);
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
            => sentMessages;

        internal void DeliverMessage(IMessage message)
            => receivedMessages.Add(message);
    }
}