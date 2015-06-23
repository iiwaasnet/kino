using System;
using System.Collections.Generic;
using System.Linq;
using Framework;
using NetMQ;

namespace Console.Messages
{
    internal partial class MultipartMessage
    {
        private const int MinFramesCount = 7;
        private readonly List<byte[]> frames;

        internal MultipartMessage(IMessage message)
            : this(message, null)
        {
        }

        internal MultipartMessage(IMessage message, byte[] senderIdentity)
        {
            frames = BuildMessageParts(message, senderIdentity).ToList();
        }

        internal MultipartMessage(NetMQMessage message)
        {
            AssertMessage(message);

            frames = SplitMessageToFrames(message).ToList();
        }

        private IEnumerable<byte[]> SplitMessageToFrames(IEnumerable<NetMQFrame> message)
            => message.Select(m => m.Buffer).ToList();

        private IEnumerable<byte[]> BuildMessageParts(IMessage message, byte[] senderIdentity)
        {
            yield return senderIdentity ?? EmptyFrame();

            // START Routing delimiters
            yield return EmptyFrame();
            yield return EmptyFrame();
            // START Routing delimiters

            yield return GetVersionFrame(message);
            yield return GetMessageIdentityFrame(message);
            yield return GetReceiverIdentityFrame(message);
            yield return GetDistributionFrame(message);
            yield return GetCorrelationIdFrame(message);
            yield return GetEndOfFlowIdentityFrame(message);
            yield return GetEndOfFlowReceiverIdentityFrame(message);
            yield return GetTTLFrame(message);

            yield return EmptyFrame();

            yield return GetMessageBodyFrame(message);
        }

        private byte[] GetReceiverIdentityFrame(IMessage message)
            => message.EndOfFlowReceiverIdentity ?? EmptyFrame();

        private byte[] GetEndOfFlowReceiverIdentityFrame(IMessage message)
            => message.EndOfFlowReceiverIdentity ?? EmptyFrame();

        private byte[] GetEndOfFlowIdentityFrame(IMessage message)
            => message.EndOfFlowIdentity ?? EmptyFrame();

        private byte[] GetCorrelationIdFrame(IMessage message)
            => message.CorrelationId ?? EmptyFrame();

        private byte[] GetTTLFrame(IMessage message)
            => message.TTL.GetBytes();

        private byte[] GetVersionFrame(IMessage message)
            => message.Version.GetBytes();

        private byte[] GetDistributionFrame(IMessage message)
            => ((int) message.Distribution).GetBytes();

        private static byte[] EmptyFrame()
            => new byte[0];

        private byte[] GetMessageBodyFrame(IMessage message)
            => message.Body;

        private byte[] GetMessageIdentityFrame(IMessage message)
            => message.Identity.GetBytes();


        private static void AssertMessage(NetMQMessage message)
        {
            if (message.FrameCount < MinFramesCount)
            {
                throw new Exception(
                    $"FrameCount expected (at least): [{MinFramesCount}], received: [{message.FrameCount}]");
            }
        }

        internal void PushRouterIdentity(byte[] routerId)
        {
            frames.Insert(ReversedFrames.NextRouterInsertPosition, routerId);
        }

        internal void SetSocketIdentity(byte[] socketId)
        {
            frames[ForwardFrames.SocketIdentity] = socketId;
        }

        internal byte[] GetSocketIdentity()
            => frames[ForwardFrames.SocketIdentity];

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

        internal byte[] GetEndOfFlowReceiverIdentity()
            => frames[frames.Count - ReversedFrames.EndOfFlowReceiverIdentity];

        internal byte[] GetEndOfFlowIdentity()
            => frames[frames.Count - ReversedFrames.EndOfFlowIdentity];

        internal byte[] GetCorrelationId()
            => frames[frames.Count - ReversedFrames.CorrelationId];

        internal byte[] GetReceiverIdentity()
            => frames[frames.Count - ReversedFrames.ReceiverIdentity];

        internal IEnumerable<byte[]> Frames => frames;
    }
}