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
        private readonly SocketConfiguration config;
        private static readonly TimeSpan ReceiveWaitTimeout;

        static Socket()
        {
            ReceiveWaitTimeout = TimeSpan.FromSeconds(3);
        }

        internal Socket(NetMQSocket socket, SocketConfiguration config)
        {
            socket.Options.Linger = config.Linger;
            socket.Options.ReceiveHighWatermark = config.ReceivingHighWatermark;
            socket.Options.SendHighWatermark = config.SendingHighWatermark;
            this.socket = socket;
            this.config = config;
        }

        public void SendMessage(IMessage message)
        {
            var multipart = new MultipartMessage((Message) message);
            var frames = (IList<byte[]>) multipart.Frames;
            var msg = new Msg();
            try
            {
                var framesCount = frames.Count;
                for (var i = 0; i < framesCount;)
                {
                    var buffer = frames[i];
                    msg.InitGC(buffer, buffer.Length);
                    var sendingTimeout1 = config.SendTimeout;
                    if (!socket.TrySend(ref msg, sendingTimeout1, ++i < framesCount))
                    {
                        throw new TimeoutException($"Sending timed out after {sendingTimeout1.TotalMilliseconds} ms!");
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
                        if (socket.TryReceive(ref msg, ReceiveWaitTimeout))
                        {
                            frames.Add(msg.Data);
                        }
                    } while (msg.HasMore);

                    if (frames.Count > 0)
                    {
                        ReceiveRate?.Increment();
                        return Message.FromMultipartMessage(new MultipartMessage(frames));
                    }
                }
                finally
                {
                    msg.Close();
                }
            }

            return null;
        }

        public void Connect(Uri address, bool waitUntilConnected = false)
        {
            socket.Connect(address.ToSocketAddress());
            if (waitUntilConnected)
            {
                config.ConnectionEstablishmentTime.Sleep();
            }
        }

        public void Disconnect(Uri address)
            => socket.Disconnect(address.ToSocketAddress());

        public void Bind(Uri address)
            => socket.Bind(address.ToSocketAddress());

        public void Unbind(Uri address)
            => socket.Unbind(address.ToSocketAddress());

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