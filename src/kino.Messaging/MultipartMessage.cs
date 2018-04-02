using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core;
using kino.Core.Framework;

namespace kino.Messaging
{
    internal partial class MultipartMessage
    {
        private const ushort FramesPerCallbackEntry = 3;
        private const ushort FramesPerRoutingEntry = 2;
        private readonly IList<byte[]> wireFrames;
        private readonly IList<byte[]> messageFrames;
        private static readonly byte[] EmptyFrame = new byte[0];

        internal MultipartMessage(Message message)
            => wireFrames = GetWireFrames(message);

        internal MultipartMessage(IList<byte[]> wireFrames)
            => messageFrames = GetMessageFrames(wireFrames);

        private IList<byte[]> GetMessageFrames(IList<byte[]> wireFrames)
        {
            var frames = new List<byte[]>();
            frames.AddRange(wireFrames.Take(wireFrames.Count -1));

            var metaFrame = new Span<byte>(wireFrames[wireFrames.Count - 1]);

            var metaFramePointer = 0;
            var routingFramesCount = metaFrame.Slice(metaFramePointer, 4).ToArray().GetInt() * 2;
            metaFramePointer += 4;
            for (var i = 0; i < routingFramesCount; i++)
            {
                var size = metaFrame.Slice(metaFramePointer, 4).ToArray().GetInt();
                metaFramePointer += 4;
                frames.Add(metaFrame.Slice(metaFramePointer, size).ToArray());
                metaFramePointer += size;
            }

            var callbackPointCount = metaFrame.Slice(metaFramePointer, 4).ToArray().GetInt() * 3;
            metaFramePointer += 4;
            for (var i = 0; i < callbackPointCount; i++)
            {
                var size = metaFrame.Slice(metaFramePointer, 4).ToArray().GetInt();
                metaFramePointer += 4;
                frames.Add(metaFrame.Slice(metaFramePointer, size).ToArray());
                metaFramePointer += size;
            }

            for (var i = 0; i < 17; i++)
            {
                var size = metaFrame.Slice(metaFramePointer, 4).ToArray().GetInt();
                metaFramePointer += 4;
                frames.Add(metaFrame.Slice(metaFramePointer, size).ToArray());
                metaFramePointer += size;
            }

            return frames;
        }

        private IList<byte[]> GetWireFrames(Message message)
        {
            var frames = new List<byte[]>(50);

            frames.Add(GetSocketIdentity(message));
            frames.Add(EmptyFrame);
            frames.Add(GetMessageBodyFrame(message));

            frames.Add(GetMetaFrame(message));

            return frames;
        }

        private byte[] GetMetaFrame(Message message)
        {
            var frames = new List<byte[]>();

            var messageRouting = message.GetMessageRouting();
            frames.Add(((int) messageRouting.Count()).GetBytes());

            foreach (var route in messageRouting)
            {
                // NOTE: New frames come here
                var bytes = route.Uri.ToSocketAddress().GetBytes();
                frames.Add(((int)bytes.Count()).GetBytes());
                frames.Add(bytes);

                frames.Add(((int)route.Identity.Count()).GetBytes());
                frames.Add(route.Identity);
            }

            frames.Add(((int) message.CallbackPoint.Count()).GetBytes());
            foreach (var callback in message.CallbackPoint)
            {
                // NOTE: New frames come here
                frames.Add(((int)callback.Partition.Count()).GetBytes());
                frames.Add(callback.Partition);

                var bytes = callback.Version.GetBytes();
                frames.Add(((int)bytes.Count()).GetBytes());
                frames.Add(bytes);

                frames.Add(((int)callback.Identity.Count()).GetBytes());
                frames.Add(callback.Identity);
            }

            //TODO: Optimize calculation of body, callbacks and routing frames offsets

            foreach (var metaFrame in GetMetaFrames(message))
            {
                frames.Add(((int) metaFrame.Length).GetBytes());
                frames.Add(metaFrame);
            }

            //frames.Add(GetCallbackReceiverNodeIdentityFrame(message)); // 17
            //frames.Add(GetCallbackKeyFrame(message)); // 16
            //frames.Add(GetDomainFrame(message)); // 15
            //frames.Add(GetSignatureFrame(message)); // 14
            //frames.Add(GetRoutingDescriptionFrame(message)); // 13
            //frames.Add(GetCallbackDescriptionFrame(message)); // 12
            //frames.Add(GetReceiverIdentityFrame(message)); // 11
            //frames.Add(GetCallbackReceiverIdentityFrame(message)); // 10
            //frames.Add(GetReceiverNodeIdentityFrame(message)); // 9
            //frames.Add(GetPartitionFrame(message)); // 8
            //frames.Add(GetVersionFrame(message)); // 7
            //frames.Add(GetMessageIdentityFrame(message)); // 6
            //frames.Add(GetTraceOptionsDistributionFrame(message)); // 5
            //frames.Add(GetCorrelationIdFrame(message)); // 4
            //frames.Add(GetTTLFrame(message)); // 3
            //frames.Add(GetMessageBodyDescriptionFrame(message)); // 2
            //frames.Add(GetWireFormatVersionFrame(message)); // 1}

            return Concatenate(frames);
        }

        private IEnumerable<byte[]> GetMetaFrames(Message message)
        {
            yield return GetCallbackReceiverNodeIdentityFrame(message); // 17
            yield return GetCallbackKeyFrame(message); // 16
            yield return GetDomainFrame(message); // 15
            yield return GetSignatureFrame(message); // 14
            yield return GetRoutingDescriptionFrame(message); // 13
            yield return GetCallbackDescriptionFrame(message); // 12
            yield return GetReceiverIdentityFrame(message); // 11
            yield return GetCallbackReceiverIdentityFrame(message); // 10
            yield return GetReceiverNodeIdentityFrame(message); // 9
            yield return GetPartitionFrame(message); // 8
            yield return GetVersionFrame(message); // 7
            yield return GetMessageIdentityFrame(message); // 6
            yield return GetTraceOptionsDistributionFrame(message); // 5
            yield return GetCorrelationIdFrame(message); // 4
            yield return GetTTLFrame(message); // 3
            yield return GetMessageBodyDescriptionFrame(message); // 2
            yield return GetWireFormatVersionFrame(message); // 1}
        }

        private byte[] Concatenate(IEnumerable<byte[]> frames)
        {
            var rv = new byte[frames.Sum(a => a.Length)];
            var offset = 0;
            foreach (var array in frames)
            {
                Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }

            return rv;
        }

        private byte[] GetMessageBodyDescriptionFrame(Message message)
        {
            ushort entryCount = 1;
            var offset = GetMessageBodyStartFrame(message, entryCount);

            return DataEncoder.Combine(offset, entryCount)
                              .GetBytes();
        }

        private ushort GetMessageBodyStartFrame(Message message, ulong entryCount)
            => (ushort) ((entryCount > 0)
                             ? GetMessageBodyStartFrameIndex(message)
                             : 0);

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
            => (ushort) ((routingEnreyCount > 0)
                             ? GetRoutingStartFrameIndex(message)
                             : 0);

        private ushort GetRoutingStartFrameIndex(Message message)
        {
            var callbacksFrameCount = message.CallbackPoint.Count() * FramesPerCallbackEntry;
            var callbacksStartFrameIndex = GetLastFixedFrameIndex() + 1;

            return (ushort) (callbacksStartFrameIndex + callbacksFrameCount);
        }

        private byte[] GetCallbackKeyFrame(Message message)
            => message.CallbackKey.GetBytes();

        private byte[] GetDomainFrame(Message message)
            => message.Domain.GetBytes();

        private byte[] GetSignatureFrame(Message message)
            => message.Signature ?? EmptyFrame;

        private byte[] GetSocketIdentity(Message message)
            => message.SocketIdentity ?? EmptyFrame;

        private byte[] GetReceiverIdentityFrame(Message message)
            => message.ReceiverIdentity ?? EmptyFrame;

        private byte[] GetCallbackReceiverIdentityFrame(Message message)
            => message.CallbackReceiverIdentity ?? EmptyFrame;

        private byte[] GetCallbackReceiverNodeIdentityFrame(Message message)
            => message.CallbackReceiverNodeIdentity ?? EmptyFrame;

        private byte[] GetWireFormatVersionFrame(Message message)
            => message.WireFormatVersion.GetBytes();

        private byte[] GetCorrelationIdFrame(IMessage message)
            => message.CorrelationId ?? EmptyFrame;

        private byte[] GetTTLFrame(IMessage message)
            => message.TTL.GetBytes();

        private byte[] GetVersionFrame(IMessage message)
            => message.Version.GetBytes();

        private byte[] GetMessageBodyFrame(IMessage message)
            => message.Body;

        private byte[] GetMessageIdentityFrame(IMessage message)
            => message.Identity;

        private byte[] GetPartitionFrame(IMessage message)
            => message.Partition ?? EmptyFrame;

        private byte[] GetReceiverNodeIdentityFrame(Message message)
            => message.ReceiverNodeIdentity ?? EmptyFrame;

        //======================================================================

        internal byte[] GetMessageIdentity()
            => messageFrames[messageFrames.Count - ReversedFramesV5.Identity];

        internal byte[] GetMessagePartition()
            => messageFrames[messageFrames.Count - ReversedFramesV5.Partition];

        internal byte[] GetMessageVersion()
            => messageFrames[messageFrames.Count - ReversedFramesV5.Version];

        internal byte[] GetMessageBody()
        {
            var data = messageFrames[messageFrames.Count - ReversedFramesV5.BodyDescription].GetULong();
            var offset = data.Split16();

            return messageFrames[messageFrames.Count - offset];
        }

        internal TimeSpan GetMessageTTL()
            => messageFrames[messageFrames.Count - ReversedFramesV5.TTL].GetTimeSpan();

        internal (MessageTraceOptions, DistributionPattern) GetTraceOptionsDistributionPattern()
        {
            var data = messageFrames[messageFrames.Count - ReversedFramesV5.TraceOptionsDistributiomPattern].GetULong();
            (var v1, var v2) = data.Split32();

            return ((MessageTraceOptions) v1, (DistributionPattern) v2);
        }

        public long GetCallbackKey()
            => messageFrames[messageFrames.Count - ReversedFramesV5.CallbackKey].GetLong();

        internal string GetDomain()
            => messageFrames[messageFrames.Count - ReversedFramesV5.Domain].GetString();

        internal byte[] GetSignature()
            => messageFrames[messageFrames.Count - ReversedFramesV5.Signature];

        internal byte[] GetCallbackReceiverIdentity()
            => messageFrames[messageFrames.Count - ReversedFramesV5.CallbackReceiverIdentity];

        internal byte[] GetCallbackReceiverNodeIdentity()
            => messageFrames[messageFrames.Count - ReversedFramesV5.CallbackReceiverNodeIdentity];

        internal byte[] GetCorrelationId()
            => messageFrames[messageFrames.Count - ReversedFramesV5.CorrelationId];

        internal byte[] GetReceiverIdentity()
            => messageFrames[messageFrames.Count - ReversedFramesV5.ReceiverIdentity];

        internal byte[] GetWireFormatVersion()
            => messageFrames[messageFrames.Count - ReversedFramesV5.WireFormatVersion];

        internal byte[] GetReceiverNodeIdentity()
            => messageFrames[messageFrames.Count - ReversedFramesV5.ReceiverNodeIdentity];

        internal IEnumerable<byte[]> Frames => wireFrames;

        internal IEnumerable<MessageIdentifier> GetCallbackPoints()
        {
            var data = messageFrames[messageFrames.Count - ReversedFramesV5.CallbackDescription].GetULong();

            var (offset, entryCount, frameDivisor) = data.Split48();
            var startIndex = messageFrames.Count - offset;
            var frameCount = entryCount * frameDivisor;

            var callbacks = new List<MessageIdentifier>();
            if (entryCount > 0)
            {
                var endIndex = startIndex - frameCount;
                while (startIndex > endIndex)
                {
                    var identity = messageFrames[startIndex];
                    var version = messageFrames[startIndex - 1].GetUShort();
                    var partition = messageFrames[startIndex - 2];

                    callbacks.Add(new MessageIdentifier(identity, version, partition));

                    startIndex -= frameDivisor;
                }
            }

            return callbacks;
        }

        internal (List<SocketEndpoint> routing, ushort hops) GetMessageRouting()
        {
            var data = messageFrames[messageFrames.Count - ReversedFramesV5.RoutingDescription].GetULong();

            var (offset, entryCount, frameDivisor, hops) = data.Split64();
            var startIndex = messageFrames.Count - offset;
            var frameCount = entryCount * frameDivisor;

            var routing = new List<SocketEndpoint>();
            if (entryCount > 0)
            {
                var endIndex = startIndex - frameCount;
                while (startIndex > endIndex)
                {
                    var identity = messageFrames[startIndex];
                    var uri = new Uri(messageFrames[startIndex - 1].GetString());

                    routing.Add(new SocketEndpoint(uri, identity));

                    startIndex -= frameDivisor;
                }
            }

            return (routing, hops);
        }
    }
}