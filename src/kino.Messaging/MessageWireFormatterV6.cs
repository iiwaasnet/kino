using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using kino.Core;
using kino.Core.Framework;

namespace kino.Messaging
{
    public class MessageWireFormatterV6 : IMessageWireFormatter
    {
        private static readonly ushort BodyFirstFrameOffset = 2;
        private static readonly ushort BodyFrameCount = 1;
        private static readonly byte[] EmptyFrame = new byte[0];

        public IList<byte[]> Serialize(IMessage message)
        {
            var msg = message.Cast<Message>();

            var frames = new List<byte[]>();

            frames.Add(msg.SocketIdentity ?? EmptyFrame);
            frames.Add(EmptyFrame);
            frames.Add(msg.Body);
            frames.Add(GetMetaFrame(msg));

            return frames;
        }

        private byte[] GetMetaFrame(Message msg)
        {
            var metaFrame = new List<byte[]>(50);

            metaFrame.Add(Versioning.WireFormatV6.GetBytes());
            AddByteArray(metaFrame, msg.Partition);
            metaFrame.Add(msg.Version.GetBytes());
            AddByteArray(metaFrame, msg.Identity);
            AddByteArray(metaFrame, msg.ReceiverIdentity);
            AddByteArray(metaFrame, msg.ReceiverNodeIdentity);
            metaFrame.Add(DataEncoder.Combine((ushort) msg.TraceOptions, (ushort) msg.Distribution).GetBytes());
            AddByteArray(metaFrame, msg.CallbackReceiverNodeIdentity);
            metaFrame.Add(msg.CallbackKey.GetBytes());
            AddString(metaFrame, msg.Domain);
            AddByteArray(metaFrame, msg.Signature);
            //
            var messageRouting = msg.GetMessageRouting();
            metaFrame.Add(DataEncoder.Combine((ushort) messageRouting.Count(), msg.Hops).GetBytes());
            AddRouting(metaFrame, messageRouting);
            //
            var callbackPoints = msg.CallbackPoint;
            metaFrame.Add(((ushort) callbackPoints.Count()).GetBytes());
            AddCallbacks(metaFrame, callbackPoints);
            //
            AddByteArray(metaFrame, msg.CallbackReceiverIdentity);
            AddByteArray(metaFrame, msg.CorrelationId);
            metaFrame.Add(msg.TTL.GetBytes());
            metaFrame.Add(DataEncoder.Combine(BodyFirstFrameOffset, BodyFrameCount).GetBytes());
            AddByteArray(metaFrame, msg.Body);

            return Concatenate(metaFrame);
        }

        private static void AddCallbacks(List<byte[]> metaFrame, IEnumerable<MessageIdentifier> callbackPoints)
        {
            foreach (var callback in callbackPoints)
            {
                var entryBuffer = new List<byte[]>();

                AddByteArray(entryBuffer, callback.Partition);
                entryBuffer.Add(callback.Version.GetBytes());
                AddByteArray(entryBuffer, callback.Identity);

                metaFrame.Add(GetResultBufferSize(entryBuffer).GetBytes());
                metaFrame.AddRange(entryBuffer);
            }
        }

        private static void AddRouting(List<byte[]> metaFrame, IEnumerable<SocketEndpoint> messageRouting)
        {
            foreach (var socketEndpoint in messageRouting)
            {
                var entryBuffer = new List<byte[]>();

                AddString(entryBuffer, socketEndpoint.Uri);
                AddByteArray(entryBuffer, socketEndpoint.Identity);

                metaFrame.Add(GetResultBufferSize(entryBuffer).GetBytes());
                metaFrame.AddRange(entryBuffer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddByteArray(ICollection<byte[]> metaFrame, byte[] bytes)
        {
            bytes = bytes ?? EmptyFrame;
            metaFrame.Add(((int) bytes.Length).GetBytes());
            metaFrame.Add(bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddString(ICollection<byte[]> metaFrame, string str)
        {
            var bytes = str.GetBytes();
            metaFrame.Add(((int) bytes.Length).GetBytes());
            metaFrame.Add(bytes);
        }

        private static byte[] Concatenate(IList<byte[]> frames)
        {
            var rv = new byte[GetResultBufferSize(frames)];
            var offset = 0;
            foreach (var array in frames)
            {
                Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }

            return rv;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetResultBufferSize(IList<byte[]> frames)
        {
            var size = 0;
            for (var i = 0; i < frames.Count; i++)
            {
                size += frames[i].Length;
            }

            return size;
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
            message.CopyMessageRouting(socketEndpoints);
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
}