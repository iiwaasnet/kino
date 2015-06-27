using System;
using System.Collections.Generic;
using System.Linq;
using NetMQ;

namespace Console.Messages
{
    internal partial class MultipartMessage
    {
        private const int MinFramesCount = 7;
        private readonly List<byte[]> frames;
        private static readonly byte[] EmptyFrame = new byte[0];

        internal MultipartMessage(IMessage message)
            : this(message, null)
        {
        }

        internal MultipartMessage(IMessage message, byte[] senderIdentity)
        {
            frames = BuildMessageParts(message, senderIdentity).ToList();
        }

        internal MultipartMessage(NetMQMessage message, bool skipIdentityFrame = false)
        {
            AssertMessage(message);

            frames = SplitMessageToFrames(message, skipIdentityFrame).ToList();
        }

        private IEnumerable<byte[]> SplitMessageToFrames(IEnumerable<NetMQFrame> message, bool skipIdentityFrame)
            => message.Skip(skipIdentityFrame ? 1 : 0).Select(m => m.Buffer).ToList();

        private IEnumerable<byte[]> BuildMessageParts(IMessage message, byte[] senderIdentity)
        {
            yield return senderIdentity ?? EmptyFrame;

            // START Routing delimiters
            yield return EmptyFrame;
            yield return EmptyFrame;
            // START Routing delimiters

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

        internal byte[] GetCallbackReceiverIdentity()
            => frames[frames.Count - ReversedFrames.CallbackReceiverIdentity];

        internal byte[] GetCallbackIdentity()
            => frames[frames.Count - ReversedFrames.CallbackIdentity];

        internal byte[] GetCorrelationId()
            => frames[frames.Count - ReversedFrames.CorrelationId];

        internal byte[] GetReceiverIdentity()
            => frames[frames.Count - ReversedFrames.ReceiverIdentity];

        internal IEnumerable<byte[]> Frames => frames;
    }
}