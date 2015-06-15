using System;
using System.Collections.Generic;
using System.Linq;
using Console.Framework;
using NetMQ;

namespace Console.Messages
{
    internal class MultipartMessage
    {
        public MultipartMessage(IMessage message)
        {
            Frames = BuildMessageParts(message).ToArray();
        }

        public MultipartMessage(NetMQMessage message)
        {
            AssertMessage(message);

            Frames = SplitMessageToFrames(message);
        }

        private IEnumerable<byte[]> SplitMessageToFrames(IEnumerable<NetMQFrame> message)
        {
            return message.Select(m => m.Buffer).ToArray();
        }

        private IEnumerable<byte[]> BuildMessageParts(IMessage message)
        {
            yield return EmptyFrame();
            yield return BuildMessageType(message);
            yield return EmptyFrame();
            yield return BuildMessageBody(message);
        }

        private static byte[] EmptyFrame()
        {
            return new byte[0];
        }

        private byte[] BuildMessageBody(IMessage message)
        {
            return message.Content;
        }

        private byte[] BuildMessageType(IMessage message)
        {
            return message.Type.GetBytes();
        }


        private static void AssertMessage(NetMQMessage message)
        {
            if (message.FrameCount < 4)
            {
                throw new Exception($"Inconsistent message received! FrameCount: [{message.FrameCount}]");
            }
        }


        internal string GetMessageType()
        {
            return Frames.Second().GetString();
        }

        internal byte[] GetMessageTypeBytes()
        {
            return Frames.Second();
        }

        internal byte[] GetMessage()
        {
            return Frames.Skip(3).Aggregate(new byte[0], (seed, array) => seed.Concat(array).ToArray());
        }

        internal IEnumerable<byte[]> Frames { get; }
    }
}