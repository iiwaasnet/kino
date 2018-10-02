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
        private readonly IList<ArraySegment<byte>> messageFrames;
        private static readonly byte[] EmptyFrame = new byte[0];

        //internal MultipartMessage(IList<byte[]> wireFrames)
        //    => messageFrames = GetMessageFrames(wireFrames);

        //internal static Message FromMultipartMessage(MultipartMessage multipartMessage)
        //{
        //    var (traceOptions, distributionPattern) = multipartMessage.GetTraceOptionsDistributionPattern();
        //    var (routes, hops) = multipartMessage.GetMessageRouting();

        //    return new Message(multipartMessage.GetMessageIdentity(),
        //                       multipartMessage.GetMessageVersion().GetUShort(),
        //                       multipartMessage.GetMessagePartition())
        //           {
        //               WireFormatVersion = multipartMessage.GetWireFormatVersion().GetInt(),
        //               Body = multipartMessage.GetMessageBody(),
        //               TTL = multipartMessage.GetMessageTTL(),
        //               CorrelationId = multipartMessage.GetCorrelationId(),
        //               Signature = multipartMessage.GetSignature(),
        //               Domain = multipartMessage.GetDomain(),
        //               TraceOptions = traceOptions,
        //               Distribution = distributionPattern,
        //               CallbackReceiverIdentity = multipartMessage.GetCallbackReceiverIdentity(),
        //               CallbackReceiverNodeIdentity = multipartMessage.GetCallbackReceiverNodeIdentity(),
        //               CallbackPoint = multipartMessage.GetCallbackPoints(),
        //               CallbackKey = multipartMessage.GetCallbackKey(),
        //               ReceiverNodeIdentity = multipartMessage.GetReceiverNodeIdentity(),
        //               ReceiverIdentity = multipartMessage.GetReceiverIdentity(),
        //               routing = routes,
        //               Hops = hops
        //           };
        //}

        internal static IMessage CreateMessage(IList<byte[]> wireFrames)
        {
            var metaFrame = new Span<byte>(wireFrames[wireFrames.Count - 1]);
            var metaFramePointer = 0;

            var size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            var messageWireVersion = metaFrame.Slice(metaFramePointer, size).GetInt();
            metaFramePointer += size;

            size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            var partition = metaFrame.Slice(metaFramePointer, size).ToArray();
            metaFramePointer += size;

            size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            var version = metaFrame.Slice(metaFramePointer, size).GetUShort();
            metaFramePointer += size;

            size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            var identity = metaFrame.Slice(metaFramePointer, size).ToArray();
            metaFramePointer += size;

            var message = new Message(identity, version, partition)
                          {
                              WireFormatVersion = messageWireVersion,
                              Body = Concatenate(wireFrames.Skip(2)
                                                           .Take(wireFrames.Count - 1))
                          };

            size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            message.CallbackReceiverNodeIdentity = metaFrame.Slice(metaFramePointer, size).ToArray();
            metaFramePointer += size;

            size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            message.CallbackKey = metaFrame.Slice(metaFramePointer, size).GetLong();
            metaFramePointer += size;

            size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            message.Domain = metaFrame.Slice(metaFramePointer, size).GetString();
            metaFramePointer += size;

            size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            message.Signature = metaFrame.Slice(metaFramePointer, size).ToArray();
            metaFramePointer += size;

            size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            var routingCount = 0;
            (routingCount, message.Hops) = metaFrame.Slice(metaFramePointer, size).GetULong().Split32();
            metaFramePointer += size;

            size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            var callbackCount = metaFrame.Slice(metaFramePointer, size).GetUShort();
            metaFramePointer += size;

            size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            message.ReceiverIdentity = metaFrame.Slice(metaFramePointer, size).ToArray();
            metaFramePointer += size;

            size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            message.CallbackReceiverIdentity = metaFrame.Slice(metaFramePointer, size).ToArray();
            metaFramePointer += size;

            size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            message.ReceiverNodeIdentity = metaFrame.Slice(metaFramePointer, size).ToArray();
            metaFramePointer += size;

            size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            var (traceOptions, distribution) = metaFrame.Slice(metaFramePointer, size).GetULong().Split32();
            message.TraceOptions = (MessageTraceOptions) traceOptions;
            message.Distribution = (DistributionPattern)distribution;
            metaFramePointer += size;

            size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            message.CorrelationId = metaFrame.Slice(metaFramePointer, size).ToArray();
            metaFramePointer += size;

            size = metaFrame.Slice(metaFramePointer, 4).GetInt();
            metaFramePointer += 4;
            message.TTL = metaFrame.Slice(metaFramePointer, size).GetTimeSpan();
            metaFramePointer += size;

            //var routingFramesCount = metaFrame.Slice(metaFramePointer, 4).GetInt();
            //metaFramePointer += 4;
            var routing = new List<SocketEndpoint>(routingCount);
            for (var i = 0; i < routingCount; i++)
            {
                size = metaFrame.Slice(metaFramePointer, 4).GetInt();
                metaFramePointer += 4;
                //frames.Add(metaFrame.Slice(metaFramePointer, size));
                var uri = new Uri(metaFrame.Slice(metaFramePointer, size).GetString());
                metaFramePointer += size;

                size = metaFrame.Slice(metaFramePointer, 4).GetInt();
                metaFramePointer += 4;
                //frames.Add(metaFrame.Slice(metaFramePointer, size));
                identity = metaFrame.Slice(metaFramePointer, size).ToArray();
                metaFramePointer += size;

                routing.Add(new SocketEndpoint(uri, identity));
            }

            message.CopyMessageRouting(routing);

            //var callbackPointCount = metaFrame.Slice(metaFramePointer, 4).GetInt() * 3;
            //metaFramePointer += 4;
            var callbacks = new List<MessageIdentifier>();
            for (var i = 0; i < callbackCount; i++)
            {
                size = metaFrame.Slice(metaFramePointer, 4).GetInt();
                metaFramePointer += 4;
                identity = metaFrame.Slice(metaFramePointer, size).ToArray();
                metaFramePointer += size;

                size = metaFrame.Slice(metaFramePointer, 4).GetInt();
                metaFramePointer += 4;
                version = metaFrame.Slice(metaFramePointer, size).GetUShort();
                metaFramePointer += size;

                size = metaFrame.Slice(metaFramePointer, 4).GetInt();
                metaFramePointer += 4;
                partition = metaFrame.Slice(metaFramePointer, size).ToArray();
                metaFramePointer += size;

                callbacks.Add(new MessageIdentifier(identity, version, partition));
            }

            message.CopyCallbackPoint(callbacks);

            return message;

            //return frames;

            //return new Message(multipartMessage.GetMessageIdentity(),
            //                   multipartMessage.GetMessageVersion().GetUShort(),
            //                   multipartMessage.GetMessagePartition())
            //       {
            //           //WireFormatVersion = multipartMessage.GetWireFormatVersion().GetInt(),
            //           //Body = multipartMessage.GetMessageBody(),
            //           //TTL = multipartMessage.GetMessageTTL(),
            //           //CorrelationId = multipartMessage.GetCorrelationId(),
            //           //Signature = multipartMessage.GetSignature(),
            //           //Domain = multipartMessage.GetDomain(),
            //           //TraceOptions = traceOptions,
            //           //Distribution = distributionPattern,
            //           //CallbackReceiverIdentity = multipartMessage.GetCallbackReceiverIdentity(),
            //           //CallbackReceiverNodeIdentity = multipartMessage.GetCallbackReceiverNodeIdentity(),
            //           //CallbackPoint = multipartMessage.GetCallbackPoints(),
            //           //CallbackKey = multipartMessage.GetCallbackKey(),
            //           //ReceiverNodeIdentity = multipartMessage.GetReceiverNodeIdentity(),
            //           //ReceiverIdentity = multipartMessage.GetReceiverIdentity(),
            //           //routing = routes,
            //           //Hops = hops
            //       };
        }

        internal static IEnumerable<byte[]> GetWireFrames(Message message)
        {
            var frames = new List<byte[]>(50);

            frames.Add(GetSocketIdentity(message));
            frames.Add(EmptyFrame);
            frames.Add(GetMessageBodyFrame(message));

            frames.Add(GetMetaFrame(message));

            return frames;
        }

        private static byte[] GetMetaFrame(Message message)
        {
            var frames = new List<byte[]>();

            foreach (var metaFrame in GetMetaFrames(message))
            {
                frames.Add(((int) metaFrame.Length).GetBytes());
                frames.Add(metaFrame);
            }

            var messageRouting = message.GetMessageRouting();
            frames.Add(((int) messageRouting.Count()).GetBytes());

            foreach (var route in messageRouting)
            {
                // NOTE: New frames come here
                var bytes = route.Uri.ToSocketAddress().GetBytes();
                frames.Add(((int) bytes.Count()).GetBytes());
                frames.Add(bytes);

                frames.Add(((int) route.Identity.Count()).GetBytes());
                frames.Add(route.Identity);
            }

            frames.Add(((int) message.CallbackPoint.Count()).GetBytes());
            foreach (var callback in message.CallbackPoint)
            {
                // NOTE: New frames come here
                frames.Add(((int) callback.Partition.Count()).GetBytes());
                frames.Add(callback.Partition);

                var bytes = callback.Version.GetBytes();
                frames.Add(((int) bytes.Count()).GetBytes());
                frames.Add(bytes);

                frames.Add(((int) callback.Identity.Count()).GetBytes());
                frames.Add(callback.Identity);
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

        private static IEnumerable<byte[]> GetMetaFrames(Message message)
        {
            yield return GetWireFormatVersionFrame(message); // 1}
            yield return GetPartitionFrame(message); // 8
            yield return GetVersionFrame(message); // 7
            yield return GetMessageIdentityFrame(message); // 6
            yield return GetCallbackReceiverNodeIdentityFrame(message); // 17
            yield return GetCallbackKeyFrame(message); // 16
            yield return GetDomainFrame(message); // 15
            yield return GetSignatureFrame(message); // 14
            yield return GetRoutingDescriptionFrame(message); // 13
            yield return GetCallbackDescriptionFrame(message); // 12
            yield return GetReceiverIdentityFrame(message); // 11
            yield return GetCallbackReceiverIdentityFrame(message); // 10
            yield return GetReceiverNodeIdentityFrame(message); // 9
            yield return GetTraceOptionsDistributionFrame(message); // 5
            yield return GetCorrelationIdFrame(message); // 4
            yield return GetTTLFrame(message); // 3
            //yield return GetMessageBodyDescriptionFrame(message); // 2
        }

        private static byte[] Concatenate(IEnumerable<byte[]> frames)
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
            return entryCount.GetBytes();

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

        private static byte[] GetTraceOptionsDistributionFrame(Message message)
            => DataEncoder.Combine((ushort) message.TraceOptions, (ushort) message.Distribution)
                          .GetBytes();

        private static byte[] GetCallbackDescriptionFrame(Message message)
        {
            var entryCount = (ushort) message.CallbackPoint.Count();
            return entryCount.GetBytes();

            var offset = GetCallbacksStartFrame(message);

            return DataEncoder.Combine(offset, entryCount, FramesPerCallbackEntry)
                              .GetBytes();
        }

        private static ushort GetCallbacksStartFrame(Message message)
            => (ushort) (message.CallbackPoint.Any()
                             ? (GetLastFixedFrameIndex() + 1)
                             : 0);

        private static byte[] GetRoutingDescriptionFrame(Message message)
        {
            var entryCount = (ushort) message.GetMessageRouting().Count();
            return DataEncoder.Combine(entryCount, message.Hops)
                              .GetBytes();

            var offset = GetRoutingStartFrame(message, entryCount);

            return DataEncoder.Combine(offset, entryCount, FramesPerRoutingEntry, message.Hops)
                              .GetBytes();
        }

        private static ushort GetRoutingStartFrame(Message message, ulong routingEntryCount)
            => (ushort) ((routingEntryCount > 0)
                             ? GetRoutingStartFrameIndex(message)
                             : 0);

        private static ushort GetRoutingStartFrameIndex(Message message)
        {
            var callbacksFrameCount = message.CallbackPoint.Count() * FramesPerCallbackEntry;
            var callbacksStartFrameIndex = GetLastFixedFrameIndex() + 1;

            return (ushort) (callbacksStartFrameIndex + callbacksFrameCount);
        }

        private static byte[] GetCallbackKeyFrame(Message message)
            => message.CallbackKey.GetBytes();

        private static byte[] GetDomainFrame(Message message)
            => message.Domain.GetBytes();

        private static byte[] GetSignatureFrame(Message message)
            => message.Signature ?? EmptyFrame;

        private static byte[] GetSocketIdentity(Message message)
            => message.SocketIdentity ?? EmptyFrame;

        private static byte[] GetReceiverIdentityFrame(Message message)
            => message.ReceiverIdentity ?? EmptyFrame;

        private static byte[] GetCallbackReceiverIdentityFrame(Message message)
            => message.CallbackReceiverIdentity ?? EmptyFrame;

        private static byte[] GetCallbackReceiverNodeIdentityFrame(Message message)
            => message.CallbackReceiverNodeIdentity ?? EmptyFrame;

        private static byte[] GetWireFormatVersionFrame(Message message)
            => message.WireFormatVersion.GetBytes();

        private static byte[] GetCorrelationIdFrame(IMessage message)
            => message.CorrelationId ?? EmptyFrame;

        private static byte[] GetTTLFrame(IMessage message)
            => message.TTL.GetBytes();

        private static byte[] GetVersionFrame(IMessage message)
            => message.Version.GetBytes();

        private static byte[] GetMessageBodyFrame(IMessage message)
            => message.Body;

        private static byte[] GetMessageIdentityFrame(IMessage message)
            => message.Identity;

        private static byte[] GetPartitionFrame(IMessage message)
            => message.Partition ?? EmptyFrame;

        private static byte[] GetReceiverNodeIdentityFrame(Message message)
            => message.ReceiverNodeIdentity ?? EmptyFrame;

        //======================================================================

        //internal byte[] GetMessageIdentity()
        //    => messageFrames[messageFrames.Count - ReversedFramesV5.Identity];

        //internal byte[] GetMessagePartition()
        //    => messageFrames[messageFrames.Count - ReversedFramesV5.Partition];

        //internal byte[] GetMessageVersion()
        //    => messageFrames[messageFrames.Count - ReversedFramesV5.Version];

        //internal byte[] GetMessageBody()
        //{
        //    var data = messageFrames[messageFrames.Count - ReversedFramesV5.BodyDescription].GetULong();
        //    var offset = data.Split16();

        //    return messageFrames[messageFrames.Count - offset];
        //}

        //internal TimeSpan GetMessageTTL()
        //    => messageFrames[messageFrames.Count - ReversedFramesV5.TTL].GetTimeSpan();

        //internal (MessageTraceOptions, DistributionPattern) GetTraceOptionsDistributionPattern()
        //{
        //    var data = messageFrames[messageFrames.Count - ReversedFramesV5.TraceOptionsDistributionPattern].GetULong();
        //    var (v1, v2) = data.Split32();

        //    return ((MessageTraceOptions) v1, (DistributionPattern) v2);
        //}

        //public long GetCallbackKey()
        //    => messageFrames[messageFrames.Count - ReversedFramesV5.CallbackKey].GetLong();

        //internal string GetDomain()
        //    => messageFrames[messageFrames.Count - ReversedFramesV5.Domain].GetString();

        //internal byte[] GetSignature()
        //    => messageFrames[messageFrames.Count - ReversedFramesV5.Signature];

        //internal byte[] GetCallbackReceiverIdentity()
        //    => messageFrames[messageFrames.Count - ReversedFramesV5.CallbackReceiverIdentity];

        //internal byte[] GetCallbackReceiverNodeIdentity()
        //    => messageFrames[messageFrames.Count - ReversedFramesV5.CallbackReceiverNodeIdentity];

        //internal byte[] GetCorrelationId()
        //    => messageFrames[messageFrames.Count - ReversedFramesV5.CorrelationId];

        //internal byte[] GetReceiverIdentity()
        //    => messageFrames[messageFrames.Count - ReversedFramesV5.ReceiverIdentity];

        //internal byte[] GetWireFormatVersion()
        //    => messageFrames[messageFrames.Count - ReversedFramesV5.WireFormatVersion];

        //internal byte[] GetReceiverNodeIdentity()
        //    => messageFrames[messageFrames.Count - ReversedFramesV5.ReceiverNodeIdentity];

        //internal IEnumerable<byte[]> Frames => wireFrames;

        //internal IEnumerable<MessageIdentifier> GetCallbackPoints()
        //{
        //    var data = messageFrames[messageFrames.Count - ReversedFramesV5.CallbackDescription].GetULong();

        //    var (offset, entryCount, frameDivisor) = data.Split48();
        //    var startIndex = messageFrames.Count - offset;
        //    var frameCount = entryCount * frameDivisor;

        //    var callbacks = new List<MessageIdentifier>();
        //    if (entryCount > 0)
        //    {
        //        var endIndex = startIndex - frameCount;
        //        while (startIndex > endIndex)
        //        {
        //            var identity = messageFrames[startIndex];
        //            var version = messageFrames[startIndex - 1].GetUShort();
        //            var partition = messageFrames[startIndex - 2];

        //            callbacks.Add(new MessageIdentifier(identity, version, partition));

        //            startIndex -= frameDivisor;
        //        }
        //    }

        //    return callbacks;
        //}

        //internal (List<SocketEndpoint> routing, ushort hops) GetMessageRouting()
        //{
        //    var data = messageFrames[messageFrames.Count - ReversedFramesV5.RoutingDescription].GetULong();

        //    var (offset, entryCount, frameDivisor, hops) = data.Split64();
        //    var startIndex = messageFrames.Count - offset;
        //    var frameCount = entryCount * frameDivisor;

        //    var routing = new List<SocketEndpoint>();
        //    if (entryCount > 0)
        //    {
        //        var endIndex = startIndex - frameCount;
        //        while (startIndex > endIndex)
        //        {
        //            var identity = messageFrames[startIndex];
        //            var uri = new Uri(messageFrames[startIndex - 1].GetString());

        //            routing.Add(new SocketEndpoint(uri, identity));

        //            startIndex -= frameDivisor;
        //        }
        //    }

        //    return (routing, hops);
        //}
    }
}