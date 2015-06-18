using System;
using System.Collections.Generic;
using System.Linq;
using Console.Framework;
using NetMQ;

namespace Console.Messages
{
    internal class MultipartMessage
    {
        private const int MinFramesCount = 7;
        private List<byte[]> frames;

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
            if (senderIdentity != null)
            {
                yield return senderIdentity;
                yield return EmptyFrame();
            }
            yield return EmptyFrame();

            yield return GetDistributionFrame(message);
            yield return GetVersionFrame(message);
            yield return GetTTLFrame(message);
            yield return GetMessageIdentityFrame(message);

            yield return EmptyFrame();

            yield return GetMessageBodyFrame(message);
        }

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
            frames.Insert(6, routerId);
        }

        internal byte[] GetSocketIdentity()
            => Frames.First();

        internal string GetMessageIdentity()
            => Frames.Second().GetString();

        internal byte[] GetMessageTypeBytes()
            => Frames.Second();

        internal byte[] GetMessage()
            => frames[frames.Count - 1];

        internal IEnumerable<byte[]> Frames => frames;
    }
}