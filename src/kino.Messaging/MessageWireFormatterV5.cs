using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using kino.Core;
using kino.Core.Framework;

namespace kino.Messaging
{
    public class MessageWireFormatterV5 : IMessageWireFormatter
    {
        private static readonly byte[] EmptyFrame = new byte[0];
        private const ushort FramesPerCallbackEntry = 3;
        private const ushort FramesPerRoutingEntry = 2;

        public IList<byte[]> Serialize(IMessage message)
        {
            var msg = message.As<Message>();
            var frames = new List<byte[]>(50);

            frames.Add(msg.SocketIdentity ?? EmptyFrame);
            frames.Add(EmptyFrame);
            frames.Add(msg.Body);
            foreach (var route in msg.GetMessageRouting())
            {
                // NOTE: New frames come here
                frames.Add(route.Uri.GetBytes());
                frames.Add(route.Identity);
            }
            foreach (var callback in msg.CallbackPoint)
            {
                // NOTE: New frames come here
                frames.Add(callback.Partition);
                frames.Add(callback.Version.GetBytes());
                frames.Add(callback.Identity);
            }
            //TODO: Optimize calculation of body, callbacks and routing frames offsets
            frames.Add(msg.CallbackReceiverNodeIdentity ?? EmptyFrame); // 17
            frames.Add(msg.CallbackKey.GetBytes()); // 16
            frames.Add(msg.Domain.GetBytes()); // 15
            frames.Add(msg.Signature ?? EmptyFrame); // 14
            frames.Add(GetRoutingDescriptionFrame(msg)); // 13
            frames.Add(GetCallbackDescriptionFrame(msg)); // 12
            frames.Add(msg.ReceiverIdentity ?? EmptyFrame); // 11
            frames.Add(msg.CallbackReceiverIdentity ?? EmptyFrame); // 10
            frames.Add(msg.ReceiverNodeIdentity ?? EmptyFrame); // 9
            frames.Add(msg.Partition ?? EmptyFrame); // 8
            frames.Add(msg.Version.GetBytes()); // 7
            frames.Add(msg.Identity); // 6
            frames.Add(GetTraceOptionsDistributionFrame(msg)); // 5
            frames.Add(msg.CorrelationId ?? EmptyFrame); // 4
            frames.Add(msg.TTL.GetBytes()); // 3
            frames.Add(GetMessageBodyDescriptionFrame(msg)); // 2
            frames.Add(Versioning.WireFormatV5.GetBytes()); // 1

            return frames;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] GetMessageBodyDescriptionFrame(Message message)
        {
            ushort entryCount = 1;
            var offset = GetMessageBodyStartFrame(message, entryCount);

            return DataEncoder.Combine(offset, entryCount)
                              .GetBytes();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort GetMessageBodyStartFrame(Message message, ulong entryCount)
            => (ushort) ((entryCount > 0)
                             ? GetMessageBodyStartFrameIndex(message)
                             : 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort GetMessageBodyStartFrameIndex(Message message)
        {
            var callbacksFrameCount = message.CallbackPoint.Count() * FramesPerCallbackEntry;
            var routingFrameCount = message.GetMessageRouting().Count() * FramesPerRoutingEntry;
            var callbacksStartFrameIndex = GetLastFixedFrameIndex() + 1;

            return (ushort) (callbacksStartFrameIndex + callbacksFrameCount + routingFrameCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] GetTraceOptionsDistributionFrame(Message message)
            => DataEncoder.Combine((ushort) message.TraceOptions, (ushort) message.Distribution)
                          .GetBytes();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] GetRoutingDescriptionFrame(Message message)
        {
            var entryCount = (ushort) message.GetMessageRouting().Count();
            var offset = GetRoutingStartFrame(message, entryCount);

            return DataEncoder.Combine(offset, entryCount, FramesPerRoutingEntry, message.Hops)
                              .GetBytes();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort GetRoutingStartFrame(Message message, ulong routingEnreyCount)
            => (ushort) ((routingEnreyCount > 0)
                             ? GetRoutingStartFrameIndex(message)
                             : 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort GetRoutingStartFrameIndex(Message message)
        {
            var callbacksFrameCount = message.CallbackPoint.Count() * FramesPerCallbackEntry;
            var callbacksStartFrameIndex = GetLastFixedFrameIndex() + 1;

            return (ushort) (callbacksStartFrameIndex + callbacksFrameCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort GetLastFixedFrameIndex()
            => ReversedFramesV5.CallbackReceiverNodeIdentity;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] GetCallbackDescriptionFrame(Message message)
        {
            var entryCount = (ushort) message.CallbackPoint.Count();
            var offset = GetCallbacksStartFrame(message);

            return DataEncoder.Combine(offset, entryCount, FramesPerCallbackEntry)
                              .GetBytes();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort GetCallbacksStartFrame(Message message)
            => (ushort) (message.CallbackPoint.Any()
                             ? (GetLastFixedFrameIndex() + 1)
                             : 0);

        public bool CanDeserialize(IList<byte[]> frames)
            => frames[frames.Count - ReversedFramesV5.WireFormatVersion].GetUShort() == Versioning.WireFormatV5;

        public IMessage Deserialize(IList<byte[]> frames)
        {
            var wireFormatVersion = frames[frames.Count - ReversedFramesV5.WireFormatVersion].GetInt();
            var (traceOptions, distributionPattern) = GetTraceOptionsDistributionPattern(frames);
            var (routes, hops) = GetMessageRouting(frames);

            var message = new Message(frames[frames.Count - ReversedFramesV5.Identity],
                                      frames[frames.Count - ReversedFramesV5.Version].GetUShort(),
                                      frames[frames.Count - ReversedFramesV5.Partition]);

            message.SetBody(GetMessageBody(frames));
            message.TTL = frames[frames.Count - ReversedFramesV5.TTL].GetTimeSpan();
            message.SetCorrelationId(frames[frames.Count - ReversedFramesV5.CorrelationId]);
            message.SetSignature(frames[frames.Count - ReversedFramesV5.Signature]);
            message.SetDomain(frames[frames.Count - ReversedFramesV5.Domain].GetString());
            message.TraceOptions = traceOptions;
            message.SetDistribution(distributionPattern);
            message.SetCallbackReceiverIdentity(frames[frames.Count - ReversedFramesV5.CallbackReceiverIdentity]);
            message.SetCallbackReceiverNodeIdentity(frames[frames.Count - ReversedFramesV5.CallbackReceiverNodeIdentity]);
            message.CopyCallbackPoint(GetCallbackPoints(frames));
            message.SetCallbackKey(frames[frames.Count - ReversedFramesV5.CallbackKey].GetLong());
            message.SetReceiverNodeIdentity(frames[frames.Count - ReversedFramesV5.ReceiverNodeIdentity]);
            message.SetReceiverIdentity(frames[frames.Count - ReversedFramesV5.ReceiverIdentity]);
            message.CopyMessageRouting(routes);
            message.SetHops(hops);

            return message;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IEnumerable<MessageIdentifier> GetCallbackPoints(IList<byte[]> frames)
        {
            var data = frames[frames.Count - ReversedFramesV5.CallbackDescription].GetULong();

            var (offset, entryCount, frameDivisor) = data.Split48();
            var startIndex = frames.Count - offset;
            var frameCount = entryCount * frameDivisor;

            var callbacks = new List<MessageIdentifier>();
            if (entryCount > 0)
            {
                var endIndex = startIndex - frameCount;
                while (startIndex > endIndex)
                {
                    var identity = frames[startIndex];
                    var version = frames[startIndex - 1].GetUShort();
                    var partition = frames[startIndex - 2];

                    callbacks.Add(new MessageIdentifier(identity, version, partition));

                    startIndex -= frameDivisor;
                }
            }

            return callbacks;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] GetMessageBody(IList<byte[]> frames)
        {
            var data = frames[frames.Count - ReversedFramesV5.BodyDescription].GetULong();
            var offset = data.Split16();

            return frames[frames.Count - offset];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (MessageTraceOptions, DistributionPattern) GetTraceOptionsDistributionPattern(IList<byte[]> frames)
        {
            var data = frames[frames.Count - ReversedFramesV5.TraceOptionsDistributionPattern].GetULong();
            var (v1, v2) = data.Split32();

            return ((MessageTraceOptions) v1, (DistributionPattern) v2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (List<SocketEndpoint> routing, ushort hops) GetMessageRouting(IList<byte[]> frames)
        {
            var data = frames[frames.Count - ReversedFramesV5.RoutingDescription].GetULong();

            var (offset, entryCount, frameDivisor, hops) = data.Split64();
            var startIndex = frames.Count - offset;
            var frameCount = entryCount * frameDivisor;

            var routing = new List<SocketEndpoint>();
            if (entryCount > 0)
            {
                var endIndex = startIndex - frameCount;
                while (startIndex > endIndex)
                {
                    var identity = frames[startIndex];
                    var uri = frames[startIndex - 1].GetString();

                    routing.Add(SocketEndpoint.FromTrustedSource(uri, identity));

                    startIndex -= frameDivisor;
                }
            }

            return (routing, hops);
        }
    }
}