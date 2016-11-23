using System.Collections.Generic;
using System.Linq;

namespace kino.Core
{
    public static class ReceiverIdentities
    {
        private static readonly byte ActorSignature = 1;
        private static readonly byte MessageHubSignature = 0;

        public static ReceiverIdentifier CreateForActor()
            => new ReceiverIdentifier(new byte[] {ActorSignature}.Concat(ReceiverIdentifier.CreateIdentity()).ToArray());

        public static ReceiverIdentifier CreateForMessageHub()
            => new ReceiverIdentifier(new byte[] {MessageHubSignature}.Concat(ReceiverIdentifier.CreateIdentity()).ToArray());

        public static bool IsActor(this ReceiverIdentifier identifier)
            => identifier.Identity.Last() == ActorSignature;

        public static bool IsActor(this IEnumerable<byte> identifier)
            => identifier.Last() == ActorSignature;

        public static bool IsMessageHub(this ReceiverIdentifier identifier)
            => identifier.Identity.Last() == MessageHubSignature;

        public static bool IsMessageHub(this IEnumerable<byte> identifier)
            => identifier.Last() == MessageHubSignature;
    }
}