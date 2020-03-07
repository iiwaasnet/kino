using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unsafe = kino.Core.Framework.Unsafe;

namespace kino.Core
{
    public static class ReceiverIdentities
    {
        private static readonly byte ActorSignature = 1;
        private static readonly byte MessageHubSignature = 0;
        private static readonly byte[] Empty = new byte[0];

        public static ReceiverIdentifier CreateForActor()
            => new ReceiverIdentifier(new [] {ActorSignature}.Concat(ReceiverIdentifier.CreateIdentity()).ToArray());

        public static ReceiverIdentifier CreateForMessageHub()
            => new ReceiverIdentifier(new [] {MessageHubSignature}.Concat(ReceiverIdentifier.CreateIdentity()).ToArray());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsActor(this ReceiverIdentifier identifier)
            => identifier?.Identity?.Length > 0
               && identifier.Identity[0] == ActorSignature;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsActor(this IList<byte> identifier)
            => identifier?.Count > 0
               && identifier[0] == ActorSignature;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMessageHub(this ReceiverIdentifier identifier)
            => identifier?.Identity?.Length > 0
               && identifier.Identity[0] == MessageHubSignature;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMessageHub(this IList<byte> identifier)
            => identifier?.Count > 0
               && identifier[0] == MessageHubSignature;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSet(this ReceiverIdentifier identifier)
            => identifier?.Identity != null && !Unsafe.ArraysEqual(identifier.Identity, Empty);
    }
}