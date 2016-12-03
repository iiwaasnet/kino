using System.Collections.Generic;
using System.Linq;
using kino.Core.Framework;

namespace kino.Core
{
    public static class ReceiverIdentities
    {
        private static readonly byte ActorSignature = 1;
        private static readonly byte MessageHubSignature = 0;
        private static readonly byte[] Empty = new byte[0];

        public static ReceiverIdentifier CreateForActor()
            => new ReceiverIdentifier(new byte[] {ActorSignature}.Concat(ReceiverIdentifier.CreateIdentity()).ToArray());

        public static ReceiverIdentifier CreateForMessageHub()
            => new ReceiverIdentifier(new byte[] {MessageHubSignature}.Concat(ReceiverIdentifier.CreateIdentity()).ToArray());

        public static bool IsActor(this ReceiverIdentifier identifier)
            => identifier?.Identity?.FirstOrDefault() == ActorSignature;

        public static bool IsActor(this IEnumerable<byte> identifier)
            => identifier?.FirstOrDefault() == ActorSignature;

        public static bool IsMessageHub(this ReceiverIdentifier identifier)
            => identifier?.Identity?.FirstOrDefault() == MessageHubSignature;

        public static bool IsMessageHub(this IEnumerable<byte> identifier)
            => identifier?.FirstOrDefault() == MessageHubSignature;

        public static bool IsSet(this ReceiverIdentifier identitifier)
            => identitifier?.Identity != null && !Unsafe.ArraysEqual(identitifier.Identity, Empty);
    }
}