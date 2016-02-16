using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using kino.Core.Messaging;
using kino.Core.Sockets;

namespace kino.Tests.Actors.Setup
{
    public class StubSocket : ISocket
    {
        private byte[] identity;
        private readonly BlockingCollection<IMessage> sentMessages;
        private readonly BlockingCollection<IMessage> receivedMessages;
        private bool connected;

        public StubSocket()
        {
            sentMessages = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
            receivedMessages = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
            connected = false;
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
            catch (OperationCanceledException)
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
            connected = true;
        }

        public void Disconnect(Uri address)
        {
            connected = false;
        }

        public void Bind(Uri address)
        {
        }

        public void SetMandatoryRouting(bool mandatory = true)
        {
        }

        public void SetReceiveHighWaterMark(int hwm)
        {
        }

        public void SetIdentity(byte[] identity)
            => this.identity = identity;

        public byte[] GetIdentity()
            => identity;

        public void Dispose()
        {
        }

        internal IEnumerable<IMessage> GetSentMessages()
            => sentMessages;

        internal void DeliverMessage(IMessage message)
            => receivedMessages.Add(message);

        internal bool IsConnected()
            => connected;
    }
}