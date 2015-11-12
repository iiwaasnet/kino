﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            foreach (var callback in message.CallbackPoint)
            {
                yield return callback.Version;
                yield return callback.Identity;
            }
            yield return GetRoutingEntryCountFrame(message); // 14
            yield return GetRoutingStartFrame(message); // 13
            yield return GetTraceOptionsFrame(message); // 12
            yield return GetVersionFrame(message); // 11
            yield return GetMessageIdentityFrame(message); // 10
            yield return GetReceiverIdentityFrame(message); // 9
            yield return GetDistributionFrame(message); // 8
            yield return GetCorrelationIdFrame(message); // 7
            yield return GetCallbackEntryCountFrame(message); // 6
            yield return GetCallbacksStartFrame(message); // 5
            yield return GetCallbackReceiverIdentityFrame(message); // 4
            yield return GetTTLFrame(message); // 3

            yield return EmptyFrame;

            yield return GetMessageBodyFrame(message);
        }

        private byte[] GetRoutingEntryCountFrame(Message message)
        {
            var count = message.GetMessageHops().Count();
            return (count > 0)
                       ? count.GetBytes()
                       : EmptyFrame;
        }

        private byte[] GetRoutingStartFrame(Message message)
        {
            var count = message.GetMessageHops().Count();
            return (count > 0)
                       ? GetRoutingStartFrameIndex(message).GetBytes()
                       : EmptyFrame;
        }

        private int GetRoutingStartFrameIndex(IMessage message)
        {
            var callbacksFrameCount = message.CallbackPoint.Count() * 2;
            var callbacksStartFrameIndex = ReversedFrames.MessageRoutingEntryCount + 1;

            return callbacksStartFrameIndex + callbacksFrameCount;
        }

        private byte[] GetTraceOptionsFrame(IMessage message)
            => ((long) message.TraceOptions).GetBytes();

        private byte[] GetSocketIdentity(IMessage message)
            => ((Message) message).SocketIdentity ?? EmptyFrame;

        private byte[] GetReceiverIdentityFrame(IMessage message)
            => message.ReceiverIdentity ?? EmptyFrame;

        private byte[] GetCallbackReceiverIdentityFrame(IMessage message)
            => message.CallbackReceiverIdentity ?? EmptyFrame;

        private byte[] GetCallbacksStartFrame(IMessage message)
            => message.CallbackPoint.Any()
                   ? (ReversedFrames.MessageRoutingEntryCount + 1).GetBytes()
                   : EmptyFrame;

        private byte[] GetCallbackEntryCountFrame(IMessage message)
        {
            var count = message.CallbackPoint.Count();
            return (count > 0)
                       ? count.GetBytes()
                       : EmptyFrame;
        }

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

        internal byte[] GetCorrelationId()
            => frames[frames.Count - ReversedFrames.CorrelationId];

        internal byte[] GetReceiverIdentity()
            => frames[frames.Count - ReversedFrames.ReceiverIdentity];

        internal IEnumerable<byte[]> Frames => frames;

        internal IEnumerable<MessageIdentifier> GetCallbackPoints()
        {
            var callbackFrameCount = GetEntryCount(ReversedFrames.CallbackEntryCount) * 2;
            var callbacks = new List<MessageIdentifier>();
            if (callbackFrameCount > 0)
            {
                var startIndex = frames.Count
                                 - frames[frames.Count - ReversedFrames.CallbackStartFrame].GetInt();
                var endIndex = startIndex - callbackFrameCount;
                while (startIndex > endIndex)
                {
                    var identity = frames[startIndex];
                    var version = frames[--startIndex];
                    callbacks.Add(new MessageIdentifier(version, identity));

                    --startIndex;
                }
            }

            return callbacks;
        }

        internal IEnumerable<SocketEndpoint> GetMessageHops()
        {
            var hopFrameCount = GetEntryCount(ReversedFrames.MessageRoutingEntryCount) * 2;
            var hops = new List<SocketEndpoint>();
            if (hopFrameCount > 0)
            {
                var startIndex = frames.Count
                                 - frames[frames.Count - ReversedFrames.MessageRoutingStartFrame].GetInt();
                var endIndex = startIndex - hopFrameCount;
                while (startIndex> endIndex)
                {
                    var identity = frames[startIndex];
                    var uri = new Uri(frames[--startIndex].GetString());
                    hops.Add(new SocketEndpoint(uri, identity));

                    --startIndex;
                }
            }

            return hops;
        }

        private int GetEntryCount(int entryCountOffset)
        {
            var countFrame = frames[frames.Count - entryCountOffset];
            return (Unsafe.Equals(countFrame, EmptyFrame))
                       ? 0
                       : countFrame.GetInt();
        }
    }
}