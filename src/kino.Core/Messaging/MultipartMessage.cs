using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core.Connectivity;
using kino.Core.Framework;

namespace kino.Core.Messaging
{
    internal partial class MultipartMessage
    {
        private const ushort FramesPerCallbackEntry = 3;
        private const ushort FramesPerRoutingEntry = 2;
        private readonly IList<byte[]> frames;
        private static readonly byte[] EmptyFrame = new byte[0];

        internal MultipartMessage(Message message)
        {
            frames = WriteFrames(message);
        }

        internal MultipartMessage(IList<byte[]> frames)
        {
            this.frames = frames;
        }

        private IList<byte[]> WriteFrames(Message message)
        {
            var frames = new List<byte[]>(50);

            frames.Add(GetSocketIdentity(message));
            frames.Add(EmptyFrame);
            frames.Add(GetMessageBodyFrame(message));
            foreach (var route in message.GetMessageRouting())
            {
                // NOTE: New frames come here
                frames.Add(route.Uri.ToSocketAddress().GetBytes());
                frames.Add(route.Identity);
            }
            foreach (var callback in message.CallbackPoint)
            {
                // NOTE: New frames come here
                frames.Add(callback.Partition);
                frames.Add(callback.Version);
                frames.Add(callback.Identity);
            }
            //TODO: Optimize calculation of body, callbacks and routing frames offsets
            frames.Add(GetSignatureFrame(message)); // 14
            frames.Add(GetRoutingDescriptionFrame(message)); // 13
            frames.Add(GetCallbackDescriptionFrame(message)); // 12
            frames.Add(GetReceiverIdentityFrame(message)); // 11
            frames.Add(GetCallbackReceiverIdentityFrame(message)); // 10
            frames.Add(GetReceiverNodeIdentityFrame(message)); // 9
            frames.Add(GetPartitionFrame(message)); // 8
            frames.Add(GetVersionFrame(message)); // 7
            frames.Add(GetMessageIdentityFrame(message)); // 6
            frames.Add(GetTraceOptionsDistributionFrame(message)); // 5
            frames.Add(GetCorrelationIdFrame(message)); // 4
            frames.Add(GetTTLFrame(message)); // 3
            frames.Add(GetMessageBodyDescriptionFrame(message)); // 2
            frames.Add(GetWireFormatVersionFrame(message)); // 1

            return frames;
        }

        private byte[] GetMessageBodyDescriptionFrame(Message message)
        {
            ushort entryCount = 1;
            var offset = GetMessageBodyStartFrame(message, entryCount);

            return DataEncoder.Combine(offset, entryCount)
                              .GetBytes();
        }

        private ushort GetMessageBodyStartFrame(Message message, ulong entryCount)
        {
            return (ushort) ((entryCount > 0)
                                 ? GetMessageBodyStartFrameIndex(message)
                                 : 0);
        }

        private ushort GetMessageBodyStartFrameIndex(Message message)
        {
            var callbacksFrameCount = message.CallbackPoint.Count() * FramesPerCallbackEntry;
            var routingFrameCount = message.GetMessageRouting().Count() * FramesPerRoutingEntry;
            var callbacksStartFrameIndex = GetLastFixedFrameIndex() + 1;

            return (ushort) (callbacksStartFrameIndex + callbacksFrameCount + routingFrameCount);
        }

        private byte[] GetTraceOptionsDistributionFrame(Message message)
            => DataEncoder.Combine((ushort) message.TraceOptions, (ushort) message.Distribution)
                          .GetBytes();

        private byte[] GetCallbackDescriptionFrame(Message message)
        {
            var entryCount = (ushort) message.CallbackPoint.Count();
            var offset = GetCallbacksStartFrame(message);

            return DataEncoder.Combine(offset, entryCount, FramesPerCallbackEntry)
                              .GetBytes();
        }

        private ushort GetCallbacksStartFrame(Message message)
            => (ushort) (message.CallbackPoint.Any()
                             ? (GetLastFixedFrameIndex() + 1)
                             : 0);

        private byte[] GetRoutingDescriptionFrame(Message message)
        {
            var entryCount = (ushort) message.GetMessageRouting().Count();
            var offset = GetRoutingStartFrame(message, entryCount);

            return DataEncoder.Combine(offset, entryCount, FramesPerRoutingEntry, message.Hops)
                              .GetBytes();
        }

        private ushort GetRoutingStartFrame(Message message, ulong routingEnreyCount)
        {
            return (ushort) ((routingEnreyCount > 0)
                                 ? GetRoutingStartFrameIndex(message)
                                 : 0);
        }

        private ushort GetRoutingStartFrameIndex(Message message)
        {
            var callbacksFrameCount = message.CallbackPoint.Count() * FramesPerCallbackEntry;
            var callbacksStartFrameIndex = GetLastFixedFrameIndex() + 1;

            return (ushort) (callbacksStartFrameIndex + callbacksFrameCount);
        }

        private byte[] GetSignatureFrame(Message message)
            => message.Signature ?? EmptyFrame;

        private byte[] GetSocketIdentity(Message message)
            => message.SocketIdentity ?? EmptyFrame;

        private byte[] GetReceiverIdentityFrame(Message message)
            => message.ReceiverIdentity ?? EmptyFrame;

        private byte[] GetCallbackReceiverIdentityFrame(Message message)
            => message.CallbackReceiverIdentity ?? EmptyFrame;

        private byte[] GetWireFormatVersionFrame(Message message)
            => message.WireFormatVersion.GetBytes();

        private byte[] GetCorrelationIdFrame(IMessage message)
            => message.CorrelationId ?? EmptyFrame;

        private byte[] GetTTLFrame(IMessage message)
            => message.TTL.GetBytes();

        private byte[] GetVersionFrame(IMessage message)
            => message.Version;

        private byte[] GetMessageBodyFrame(IMessage message)
            => message.Body;

        private byte[] GetMessageIdentityFrame(IMessage message)
            => message.Identity;

        private byte[] GetPartitionFrame(IMessage message)
            => message.Partition ?? EmptyFrame;

        private byte[] GetReceiverNodeIdentityFrame(Message message)
            => message.PopReceiverNode() ?? EmptyFrame;

        internal byte[] GetMessageIdentity()
            => frames[frames.Count - ReversedFramesV4.Identity];

        internal byte[] GetMessagePartition()
            => frames[frames.Count - ReversedFramesV4.Partition];

        internal byte[] GetMessageVersion()
            => frames[frames.Count - ReversedFramesV4.Version];

        internal byte[] GetMessageBody()
        {
            var data = frames[frames.Count - ReversedFramesV4.BodyDescription].GetULong();
            ushort offset;
            data.Split(out offset);

            return frames[frames.Count - offset];
        }

        internal TimeSpan GetMessageTTL()
            => frames[frames.Count - ReversedFramesV4.TTL].GetTimeSpan();

        internal void GetTraceOptionsDistributionPattern(out MessageTraceOptions traceOptions, out DistributionPattern distributionPattern)
        {
            var data = frames[frames.Count - ReversedFramesV4.TraceOptionsDistributiomPattern].GetULong();
            ushort v1, v2;
            data.Split(out v1, out v2);
            traceOptions = (MessageTraceOptions) v1;
            distributionPattern = (DistributionPattern) v2;
        }

        internal byte[] GetSignature()
            => frames[frames.Count - ReversedFramesV4.Signature];

        internal byte[] GetCallbackReceiverIdentity()
            => frames[frames.Count - ReversedFramesV4.CallbackReceiverIdentity];

        internal byte[] GetCorrelationId()
            => frames[frames.Count - ReversedFramesV4.CorrelationId];

        internal byte[] GetReceiverIdentity()
            => frames[frames.Count - ReversedFramesV4.ReceiverIdentity];

        internal byte[] GetWireFormatVersion()
            => frames[frames.Count - ReversedFramesV4.WireFormatVersion];

        internal byte[] GetReceiverNodeIdentity()
            => frames[frames.Count - ReversedFramesV4.ReceiverNodeIdentity];

        internal IEnumerable<byte[]> Frames => frames;

        internal IEnumerable<MessageIdentifier> GetCallbackPoints(int wireFormatVersion)
        {
            var data = frames[frames.Count - ReversedFramesV4.CallbackDescription].GetULong();

            ushort offset, entryCount, frameDivisor;
            data.Split(out offset, out entryCount, out frameDivisor);
            var startIndex = frames.Count - offset;
            var frameCount = entryCount * frameDivisor;

            var callbacks = new List<MessageIdentifier>();
            if (entryCount > 0)
            {
                var endIndex = startIndex - frameCount;
                while (startIndex > endIndex)
                {
                    var identity = frames[startIndex];
                    var version = frames[startIndex - 1];
                    var partition = frames[startIndex - 2];

                    callbacks.Add(new MessageIdentifier(version, identity, partition));

                    startIndex -= frameDivisor;
                }
            }

            return callbacks;
        }

        internal IEnumerable<SocketEndpoint> GetMessageRouting(out ushort hops)
        {
            var data = frames[frames.Count - ReversedFramesV4.RoutingDescription].GetULong();

            ushort offset, entryCount, frameDivisor;
            data.Split(out offset, out entryCount, out frameDivisor, out hops);
            var startIndex = frames.Count - offset;
            var frameCount = entryCount * frameDivisor;

            var routing = new List<SocketEndpoint>();
            if (entryCount > 0)
            {
                var endIndex = startIndex - frameCount;
                while (startIndex > endIndex)
                {
                    var identity = frames[startIndex];
                    var uri = new Uri(frames[startIndex - 1].GetString());

                    routing.Add(new SocketEndpoint(uri, identity));

                    startIndex -= frameDivisor;
                }
            }

            return routing;
        }
    }
}