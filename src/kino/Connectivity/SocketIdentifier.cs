using System;
using kino.Framework;

namespace kino.Connectivity
{
    public class SocketIdentifier : IEquatable<SocketIdentifier>
    {
        public SocketIdentifier(byte[] identity)
        {
            Identity = identity;
        }

        public static byte[] CreateIdentity()
            => Guid.NewGuid().ToString().GetBytes();

        public static SocketIdentifier Create()
            => new SocketIdentifier(CreateIdentity());

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            return obj.GetType() == GetType()
                   && StructuralCompare((SocketIdentifier) obj);
        }

        public override int GetHashCode()
        {
            return Identity?.Length * 397 ?? 0;
        }

        public bool Equals(SocketIdentifier other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return StructuralCompare(other);
        }

        private bool StructuralCompare(SocketIdentifier other)
            => Unsafe.Equals(Identity, other.Identity);

        public byte[] Identity { get; }
    }
}