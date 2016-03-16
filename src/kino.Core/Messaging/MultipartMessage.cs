using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core.Connectivity;
using kino.Core.Framework;

namespace kino.Core.Messaging
{
    internal partial class MultipartMessage
    {
        private readonly IList<byte[]> frames;
        private static readonly byte[] EmptyFrame = new byte[0];

        internal MultipartMessage(Message message)
        {
            frames = BuildMessageParts(message).ToList();
        }

        internal MultipartMessage(IList<byte[]> frames)
        {
            this.frames = frames;
        }

        private IEnumerable<byte[]> BuildMessageParts(Message message)
            => WriteFrames(message);

        private IEnumerable<byte[]> WriteFrames(Message message)
        {
            yield return GetSocketIdentity(message);
            yield return EmptyFrame;

            foreach (var route in message.GetMessageRouting())
            {
                yield return route.Uri.ToSocketAddress().GetBytes();
                yield return route.Identity;
            }
            foreach (var callback in message.CallbackPoint)
            {
                yield return callback.Version;
                yield return callback.Identity;
            }
            yield return GetReceiverNodeIdentityFrame(message); // 17
            yield return GetMessageHopsFrame(message); // 16
            yield return GetRoutingEntryCountFrame(message); // 15
            yield return GetRoutingStartFrame(message); // 14
            yield return GetTraceOptionsFrame(message); // 13
            yield return GetVersionFrame(message); // 12
            yield return GetMessageIdentityFrame(message); // 11
            yield return GetReceiverIdentityFrame(message); // 10
            yield return GetDistributionFrame(message); // 9
            yield return GetCorrelationIdFrame(message); // 8
            yield return GetCallbackEntryCountFrame(message); // 7
            yield return GetCallbacksStartFrame(message); // 6
            yield return GetCallbackReceiverIdentityFrame(message); // 5
            yield return GetTTLFrame(message); // 4
            yield return GetWireFormatVersionFrame(message); // 3

            yield return EmptyFrame;

            yield return GetMessageBodyFrame(message);
        }

        private byte[] GetRoutingEntryCountFrame(Message message)
        {
            var count = message.GetMessageRouting().Count();
            return (count > 0)
                       ? count.GetBytes()
                       : EmptyFrame;
        }

        private byte[] GetRoutingStartFrame(Message message)
        {
            var count = message.GetMessageRouting().Count();
            return (count > 0)
                       ? GetRoutingStartFrameIndex(message).GetBytes()
                       : EmptyFrame;
        }

        private int GetRoutingStartFrameIndex(Message message)
        {
            var callbacksFrameCount = message.CallbackPoint.Count() * 2;
            var callbacksStartFrameIndex = GetLastFixedFrameIndex() + 1;

            return callbacksStartFrameIndex + callbacksFrameCount;
        }

        private byte[] GetMessageHopsFrame(Message message)
            => message.Hops.GetBytes();

        private byte[] GetTraceOptionsFrame(IMessage message)
            => ((long) message.TraceOptions).GetBytes();

        private byte[] GetSocketIdentity(Message message)
            => message.SocketIdentity ?? EmptyFrame;

        private byte[] GetReceiverIdentityFrame(Message message)
            => message.ReceiverIdentity ?? EmptyFrame;

        private byte[] GetCallbackReceiverIdentityFrame(Message message)
            => message.CallbackReceiverIdentity ?? EmptyFrame;

        private byte[] GetCallbacksStartFrame(Message message)
            => message.CallbackPoint.Any()
                   ? (GetLastFixedFrameIndex() + 1).GetBytes()
                   : EmptyFrame;

        private byte[] GetCallbackEntryCountFrame(Message message)
        {
            var count = message.CallbackPoint.Count();
            return (count > 0)
                       ? count.GetBytes()
                       : EmptyFrame;
        }

        private byte[] GetWireFormatVersionFrame(Message message)
            => message.WireFormatVersion.GetBytes();

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

        private byte[] GetReceiverNodeIdentityFrame(Message message)
            => message.PopReceiverNode() ?? EmptyFrame;

        internal byte[] GetMessageIdentity()
            => frames[frames.Count - ReversedFramesV1.Identity];

        internal byte[] GetMessageVersion()
            => frames[frames.Count - ReversedFramesV1.Version];

        internal byte[] GetMessageBody()
            => frames[frames.Count - ReversedFramesV1.Body];

        internal byte[] GetMessageTTL()
            => frames[frames.Count - ReversedFramesV1.TTL];

        internal byte[] GetMessageDistributionPattern()
            => frames[frames.Count - ReversedFramesV1.DistributionPattern];

        internal byte[] GetTraceOptions()
            => frames[frames.Count - ReversedFramesV1.TraceOptions];

        internal byte[] GetMessageHops()
            => frames[frames.Count - ReversedFramesV1.MessageHops];

        internal byte[] GetCallbackReceiverIdentity()
            => frames[frames.Count - ReversedFramesV1.CallbackReceiverIdentity];

        internal byte[] GetCorrelationId()
            => frames[frames.Count - ReversedFramesV1.CorrelationId];

        internal byte[] GetReceiverIdentity()
            => frames[frames.Count - ReversedFramesV1.ReceiverIdentity];

        internal byte[] GetWireFormatVersion()
            => frames[frames.Count - ReversedFramesV1.WireFormatVersion];

        internal byte[] GetReceiverNodeIdentity()
            => frames[frames.Count - ReversedFramesV2.ReceiverNodeIdentity];

        internal IEnumerable<byte[]> Frames => frames;

        internal IEnumerable<MessageIdentifier> GetCallbackPoints()
        {
            var callbackFrameCount = GetEntryCount(ReversedFramesV1.CallbackEntryCount) * 2;
            var callbacks = new List<MessageIdentifier>();
            if (callbackFrameCount > 0)
            {
                var startIndex = frames.Count
                                 - frames[frames.Count - ReversedFramesV1.CallbackStartFrame].GetInt();
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

        internal IEnumerable<SocketEndpoint> GetMessageRouting()
        {
            var routingFrameCount = GetEntryCount(ReversedFramesV1.MessageRoutingEntryCount) * 2;
            var routing = new List<SocketEndpoint>();
            if (routingFrameCount > 0)
            {
                var startIndex = frames.Count
                                 - frames[frames.Count - ReversedFramesV1.MessageRoutingStartFrame].GetInt();
                var endIndex = startIndex - routingFrameCount;
                while (startIndex > endIndex)
                {
                    var identity = frames[startIndex];
                    var uri = new Uri(frames[--startIndex].GetString());
                    routing.Add(new SocketEndpoint(uri, identity));

                    --startIndex;
                }
            }

            return routing;
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