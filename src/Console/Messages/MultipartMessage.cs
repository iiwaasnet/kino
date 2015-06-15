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
            => message.Select(m => m.Buffer).ToArray();

        private IEnumerable<byte[]> BuildMessageParts(IMessage message)
        {
            yield return EmptyFrame();
            yield return BuildMessageType(message);
            yield return EmptyFrame();
            yield return BuildMessageBody(message);
        }

        private static byte[] EmptyFrame()
            => new byte[0];

        private byte[] BuildMessageBody(IMessage message)
            => message.Content;

        private byte[] BuildMessageType(IMessage message)
            => message.Type.GetBytes();


        private static void AssertMessage(NetMQMessage message)
        {
            if (message.FrameCount < 4)
            {
                throw new Exception($"Inconsistent message received! FrameCount: [{message.FrameCount}]");
            }
        }


        internal string GetMessageType() 
            => Frames.Second().GetString();

        internal byte[] GetMessageTypeBytes() 
            => Frames.Second();

        internal byte[] GetMessage() 
            => Frames.Skip(3).Aggregate(new byte[0], (seed, array) => seed.Concat(array).ToArray());

        internal IEnumerable<byte[]> Frames { get; }
    }
}