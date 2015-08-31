using System;
using System.Collections.Generic;
using System.Linq;
using kino.Connectivity;
using kino.Framework;
using NetMQ;

namespace kino.Messaging
{
    internal partial class MultipartMessage
    {
        private const int MinFramesCount = 12;
        private readonly IList<byte[]> frames;
        private static readonly byte[] EmptyFrame = new byte[0];

        internal MultipartMessage(Message message)
        {
            frames = BuildMessageParts(message).ToList();
        }

        internal MultipartMessage(NetMQMessage message)
        {
            AssertMessage(message);

            frames = SplitMessageToFrames(message);
        }

        private IList<byte[]> SplitMessageToFrames(IEnumerable<NetMQFrame> message)
            => message.Select(m => m.Buffer).ToList();

        private IEnumerable<byte[]> BuildMessageParts(Message message)
        {
            yield return GetSocketIdentity(message);
            
            yield return EmptyFrame;
            foreach (var hop in message.GetMessageHops())
            {
                yield return hop.Uri.ToSocketAddress().GetBytes();
                yield return hop.Identity;
            }
            yield return EmptyFrame;

            yield return GetTraceOptionsFrame(message);
            yield return GetVersionFrame(message);
            yield return GetMessageIdentityFrame(message);
            yield return GetReceiverIdentityFrame(message);
            yield return GetDistributionFrame(message);
            yield return GetCorrelationIdFrame(message);
            yield return GetCallbackIdentityFrame(message);
            yield return GetCallbackReceiverIdentityFrame(message);
            yield return GetTTLFrame(message);

            yield return EmptyFrame;

            yield return GetMessageBodyFrame(message);
        }

        private byte[] GetTraceOptionsFrame(IMessage message)
            => ((long) message.TraceOptions).GetBytes();

        private byte[] GetSocketIdentity(IMessage message)
            => ((Message) message).SocketIdentity ?? EmptyFrame;

        private byte[] GetReceiverIdentityFrame(IMessage message)
            => message.ReceiverIdentity ?? EmptyFrame;

        private byte[] GetCallbackReceiverIdentityFrame(IMessage message)
            => message.CallbackReceiverIdentity ?? EmptyFrame;

        private byte[] GetCallbackIdentityFrame(IMessage message)
            => message.CallbackIdentity ?? EmptyFrame;

        private byte[] GetCorrelationIdFrame(IMessage message)
            => message.CorrelationId ?? EmptyFrame;

        private byte[] GetTTLFrame(IMessage message)
            => message.TTL.GetBytes();

        private byte[] GetVersionFrame(IMessage message)
            => message.Version;

        private byte[] GetDistributionFrame(IMessage message)
            => ((int) message.Distribution).GetBytes();

        private byte[] GetMessageBodyFrame(IMessage message)
            => message.Body;

        private byte[] GetMessageIdentityFrame(IMessage message)
            => message.Identity;

        private static void AssertMessage(NetMQMessage message)
        {
            if (message.FrameCount < MinFramesCount)
            {
                throw new Exception($"FrameCount expected (at least): [{MinFramesCount}], received: [{message.FrameCount}]");
            }
        }

        internal byte[] GetMessageIdentity()
            => frames[frames.Count - ReversedFrames.Identity];

        internal byte[] GetMessageVersion()
            => frames[frames.Count - ReversedFrames.Version];

        internal byte[] GetMessageBody()
            => frames[frames.Count - ReversedFrames.Body];

        internal byte[] GetMessageTTL()
            => frames[frames.Count - ReversedFrames.TTL];

        internal byte[] GetMessageDistributionPattern()
            => frames[frames.Count - ReversedFrames.DistributionPattern];

        internal byte[] GetTraceOptions()
            => frames[frames.Count - ReversedFrames.TraceOptions];

        internal byte[] GetCallbackReceiverIdentity()
            => frames[frames.Count - ReversedFrames.CallbackReceiverIdentity];

        internal byte[] GetCallbackIdentity()
            => frames[frames.Count - ReversedFrames.CallbackIdentity];

        internal byte[] GetCorrelationId()
            => frames[frames.Count - ReversedFrames.CorrelationId];

        internal byte[] GetReceiverIdentity()
            => frames[frames.Count - ReversedFrames.ReceiverIdentity];

        internal IEnumerable<byte[]> Frames => frames;

        internal IEnumerable<SocketEndpoint> GetMessageHops()
        {
            var hops = new List<SocketEndpoint>();
            var firstHopEntry = GetFirstHopEntryIndex();
            var lastHopEntry = frames.Count - ReversedFrames.NextRouterInsertPosition;

            for (var i = firstHopEntry; i < lastHopEntry; i++)
            {
                hops.Add(new SocketEndpoint(new Uri(frames[i].GetString()),
                                            frames[++i]));
            }

            return hops;
        }

        private int GetFirstHopEntryIndex()
        {
            var index = frames.Count - ReversedFrames.NextRouterInsertPosition - 1;
            while (!Unsafe.Equals(frames[index], EmptyFrame) && 0 <= index)
            {
                index--;
            }

            return ++index;
        }
    }
}