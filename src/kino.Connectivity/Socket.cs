using System;
using System.Collections.Generic;
using System.Threading;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using NetMQ;
using NetMQ.Sockets;

namespace kino.Connectivity
{
    internal class Socket : ISocket
    {
        private readonly NetMQSocket socket;
        private readonly IMessageWireFormatter messageWireFormatter;
        private readonly SocketConfiguration config;

        internal Socket(NetMQSocket socket,
                        IMessageWireFormatter messageWireFormatter,
                        SocketConfiguration config)
        {
            socket.Options.Linger = config.Linger;
            socket.Options.ReceiveHighWatermark = config.ReceivingHighWatermark;
            socket.Options.SendHighWatermark = config.SendingHighWatermark;
            this.socket = socket;
            this.messageWireFormatter = messageWireFormatter;
            this.config = config;
        }

        public void SendMessage(IMessage message)
        {
            var frames = messageWireFormatter.Serialize((Message) message);
            var msg = new Msg();
            try
            {
                var framesCount = frames.Count;
                for (var i = 0; i < framesCount;)
                {
                    var buffer = frames[i];
                    msg.InitGC(buffer, buffer.Length);
                    var sendingTimeout = config.SendTimeout;
                    if (!socket.TrySend(ref msg, sendingTimeout, ++i < framesCount))
                    {
                        throw new TimeoutException($"Sending timed out after {sendingTimeout.TotalMilliseconds} ms!");
                    }
                }
                SendRate?.Increment();
            }
            finally
            {
                msg.Close();
            }
        }

        public IMessage ReceiveMessage(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var frames = new List<byte[]>();
                var msg = new Msg();
                msg.InitEmpty();
                try
                {
                    do
                    {
                        if (socket.TryReceive(ref msg, config.ReceiveWaitTimeout))
                        {
                            frames.Add(msg.Data);
                        }
                    } while (msg.HasMore);

                    if (frames.Count > 0)
                    {
                        ReceiveRate?.Increment();
                        return messageWireFormatter.Deserialize(frames);
                    }
                }
                finally
                {
                    msg.Close();
                }
            }

            return null;
        }

        public void Connect(string address, bool waitUntilConnected = false)
        {
            socket.Connect(address);
            if (waitUntilConnected)
            {
                config.ConnectionEstablishmentTime.Sleep();
            }
        }

        public void Disconnect(string address)
            => socket.Disconnect(address);

        public void Bind(string address)
            => socket.Bind(address);

        public void Unbind(string address)
            => socket.Unbind(address);

        public void Subscribe(string topic = "")
            => ((SubscriberSocket) socket).Subscribe(topic);

        public void Subscribe(byte[] topic)
            => ((SubscriberSocket) socket).Subscribe(topic);

        public void Unsubscribe(string topic = "")
            => ((SubscriberSocket) socket).Unsubscribe(topic);

        public void SetMandatoryRouting(bool mandatory = true)
            => socket.Options.RouterMandatory = mandatory;

        public void SetReceiveHighWaterMark(int hwm)
            => socket.Options.ReceiveHighWatermark = hwm;

        public void SetIdentity(byte[] identity)
            => socket.Options.Identity = identity;

        public byte[] GetIdentity()
            => socket.Options.Identity;

        public void Dispose()
            => socket.Dispose();

        public IPerformanceCounter ReceiveRate { get; set; }

        public IPerformanceCounter SendRate { get; set; }
    }
}