using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using kino.Core;
using kino.Core.Framework;

namespace kino.Messaging
{
#if !NET47
    public class MessageWireFormatterV6_1 : IMessageWireFormatter
    {
        private static readonly ushort BodyFirstFrameOffset = 2;
        private static readonly ushort BodyFrameCount = 1;
        private static readonly byte[] EmptyFrame = new byte[0];

        public IList<byte[]> Serialize(IMessage message)
        {
            var msg = message.Cast<Message>();

            var frames = new List<byte[]>(5);

            frames.Add(msg.SocketIdentity ?? EmptyFrame);
            frames.Add(EmptyFrame);
            frames.Add(msg.Body);
            frames.Add(GetMetaFrame(msg));

            return frames;
        }

        private byte[] GetMetaFrame(Message msg)
        {
            var buffer = new byte[CalculateMetaFrameSize(msg)];
            var pointer = new Span<byte>(buffer);

            pointer = Versioning.WireFormatV6.GetBytes(pointer);
            pointer = AddByteArray(pointer, msg.Partition);
            pointer = msg.Version.GetBytes(pointer);
            pointer = AddByteArray(pointer, msg.Identity);
            pointer = AddByteArray(pointer, msg.ReceiverIdentity);
            pointer = AddByteArray(pointer, msg.ReceiverNodeIdentity);
            pointer = DataEncoder.Combine((ushort) msg.TraceOptions, (ushort) msg.Distribution).GetBytes(pointer);
            pointer = AddByteArray(pointer, msg.CallbackReceiverNodeIdentity);
            pointer = msg.CallbackKey.GetBytes(pointer);
            pointer = AddString(pointer, msg.Domain);
            pointer = AddByteArray(pointer, msg.Signature);
            //
            var messageRouting = msg.GetMessageRouting();
            pointer = DataEncoder.Combine((ushort) messageRouting.Count(), msg.Hops).GetBytes(pointer);
            pointer = AddRouting(pointer, messageRouting);
            //
            var callbackPoints = msg.CallbackPoint;
            pointer = ((ushort) callbackPoints.Count()).GetBytes(pointer);
            pointer = AddCallbacks(pointer, callbackPoints);
            //
            pointer = AddByteArray(pointer, msg.CallbackReceiverIdentity);
            pointer = AddByteArray(pointer, msg.CorrelationId);
            pointer = msg.TTL.GetBytes(pointer);
            DataEncoder.Combine(BodyFirstFrameOffset, BodyFrameCount).GetBytes(pointer);

            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CalculateMetaFrameSize(Message msg)
            => sizeof(ushort) // WireFormatV6
               + GetArraySize(msg.Partition)
               + sizeof(ushort) // Version
               + GetArraySize(msg.Identity)
               + GetArraySize(msg.ReceiverIdentity)
               + GetArraySize(msg.ReceiverNodeIdentity)
               + sizeof(ulong)
               + GetArraySize(msg.CallbackReceiverNodeIdentity)
               + sizeof(long)
               + GetStringSize(msg.Domain)
               + GetArraySize(msg.Signature)
               + sizeof(ulong)
               + GetMessageRoutingSize(msg)
               + sizeof(ushort)
               + GetCallbacksSize(msg)
               + GetArraySize(msg.CallbackReceiverIdentity)
               + GetArraySize(msg.CorrelationId)
               + sizeof(long) // TTL
               + sizeof(ulong);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCallbacksSize(Message msg)
        {
            var size = 0;
            foreach (var callback in msg.CallbackPoint)
            {
                size += GetArraySize(callback.Partition)
                        + sizeof(ushort)
                        + GetArraySize(callback.Identity)
                        + sizeof(int);
            }

            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetMessageRoutingSize(Message msg)
        {
            var size = 0;
            foreach (var socketEndpoint in msg.GetMessageRouting())
            {
                size += GetStringSize(socketEndpoint.Address)
                        + GetArraySize(socketEndpoint.Identity)
                        + sizeof(int);
            }

            return size;
        }

        private static Span<byte> AddCallbacks(Span<byte> destination, IEnumerable<MessageIdentifier> callbackPoints)
        {
            foreach (var callback in callbackPoints)
            {
                var entrySize = GetArraySize(callback.Partition)
                                + sizeof(ushort)
                                + GetArraySize(callback.Identity);
                destination = entrySize.GetBytes(destination);

                destination = AddByteArray(destination, callback.Partition);
                destination = callback.Version.GetBytes(destination);
                destination = AddByteArray(destination, callback.Identity);
            }

            return destination;
        }

        private static Span<byte> AddRouting(Span<byte> destination, IEnumerable<NodeAddress> messageRouting)
        {
            foreach (var socketEndpoint in messageRouting)
            {
                var entrySize = GetStringSize(socketEndpoint.Address)
                                + GetArraySize(socketEndpoint.Identity);
                destination = entrySize.GetBytes(destination);

                destination = AddString(destination, socketEndpoint.Address);
                destination = AddByteArray(destination, socketEndpoint.Identity);
            }

            return destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetArraySize(ICollection bytes)
            => sizeof(int) + (bytes?.Count ?? 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetStringSize(string str)
            => sizeof(int) + (str?.Length ?? 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Span<byte> AddByteArray(Span<byte> destination, byte[] bytes)
        {
            bytes = bytes ?? EmptyFrame;
            var length = bytes.Length;

            destination = length.GetBytes(destination);

            for (var i = 0; i < length; i++)
            {
                destination[i] = bytes[i];
            }

            return destination.Slice(length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Span<byte> AddString(Span<byte> destination, string str)
        {
            destination = str.Length.GetBytes(destination);
            return str.GetBytes(destination);
        }

        public bool CanDeserialize(IList<byte[]> frames)
        {
            var metaFrame = new Span<byte>(frames[frames.Count - 1]);
            metaFrame.GetUShort(out var wireFormatVersion);

            return wireFormatVersion == Versioning.WireFormatV6;
        }

        public IMessage Deserialize(IList<byte[]> frames)
        {
            var metaFrame = new Span<byte>(frames[frames.Count - 1]);

            metaFrame = metaFrame.GetUShort(out var wireFormatVersion);
            metaFrame = GetByteArray(metaFrame, out var partition);
            metaFrame = metaFrame.GetUShort(out var version);
            metaFrame = GetByteArray(metaFrame, out var identity);
            var message = new Message(identity, version, partition);
            metaFrame = GetByteArray(metaFrame, out var receiverIdentity);
            message.SetReceiverIdentity(receiverIdentity);
            metaFrame = GetByteArray(metaFrame, out var receiverNodeIdentity);
            message.SetReceiverNodeIdentity(receiverNodeIdentity);
            metaFrame = metaFrame.GetULong(out var tmp);
            var (traceOptions, distribution) = tmp.Split32();
            message.TraceOptions = (MessageTraceOptions) traceOptions;
            message.SetDistribution((DistributionPattern) distribution);
            metaFrame = GetByteArray(metaFrame, out var callbackReceiverNodeIdentity);
            message.SetCallbackReceiverNodeIdentity(callbackReceiverNodeIdentity);
            metaFrame = metaFrame.GetLong(out var callbackKey);
            message.SetCallbackKey(callbackKey);
            metaFrame = GetString(metaFrame, out var domain);
            message.SetDomain(domain);
            metaFrame = GetByteArray(metaFrame, out var signature);
            message.SetSignature(signature);
            // Routing
            metaFrame = metaFrame.GetULong(out tmp);
            var (routingEntryCount, hops) = tmp.Split32();
            message.SetHops(hops);
            var socketEndpoints = new List<SocketEndpoint>();
            for (var i = 0; i < routingEntryCount; i++)
            {
                metaFrame = metaFrame.GetInt(out var entrySize);
                var identityFrame = GetString(metaFrame, out var uri);
                var _ = GetByteArray(identityFrame, out var id);

                socketEndpoints.Add(SocketEndpoint.FromTrustedSource(uri, id));

                metaFrame = metaFrame.Slice(entrySize);
            }

            message.CopyMessageRouting(socketEndpoints.Select(se => new NodeAddress {Address = se.Uri, Identity = se.Identity}));
            // Callbacks
            metaFrame = metaFrame.GetUShort(out var callbackEntryCount);
            var callbacks = new List<MessageIdentifier>();
            for (var i = 0; i < callbackEntryCount; i++)
            {
                metaFrame = metaFrame.GetInt(out var entrySize);
                var versionFrame = GetByteArray(metaFrame, out partition);
                var identityFrame = versionFrame.GetUShort(out version);
                var _ = GetByteArray(identityFrame, out identity);

                callbacks.Add(new MessageIdentifier(identity, version, partition));

                metaFrame = metaFrame.Slice(entrySize);
            }

            message.CopyCallbackPoint(callbacks);
            //
            metaFrame = GetByteArray(metaFrame, out var callbackReceiverIdentity);
            message.SetCallbackReceiverIdentity(callbackReceiverIdentity);
            metaFrame = GetByteArray(metaFrame, out var correlationId);
            message.SetCorrelationId(correlationId);
            metaFrame = metaFrame.GetTimeSpan(out var ttl);
            message.TTL = ttl;
            _ = metaFrame.GetULong(out tmp);
            var (firstBodyFrameOffset, bodyFrameCount) = tmp.Split32();
            // body
            message.SetBody(frames[frames.Count - firstBodyFrameOffset]);

            message.SetSocketIdentity(frames[0]);

            return message;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Span<byte> GetByteArray(Span<byte> bytes, out byte[] array)
        {
            bytes = bytes.GetInt(out var size);
            array = bytes.Slice(0, size).ToArray();

            return bytes.Slice(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Span<byte> GetString(Span<byte> bytes, out string array)
        {
            bytes = bytes.GetInt(out var size);
            array = bytes.Slice(0, size).GetString();

            return bytes.Slice(size);
        }
    }
#endif
}