using System;
using rawf.Framework;

namespace rawf.Connectivity
{
    public class SocketIdentifier : IEquatable<SocketIdentifier>
    {
        public SocketIdentifier(byte[] identity)
        {
            Identity = identity;
        }

        public static byte[] CreateNew()
            => Guid.NewGuid().ToString().GetBytes();


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
        {
            return Unsafe.Equals(Identity, other.Identity);
        }

        public byte[] Identity { get; }
    }
}