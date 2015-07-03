using System;
using rawf.Framework;

namespace rawf.Actors
{
    internal class SocketIdentifier : IEquatable<SocketIdentifier>
    {
        public SocketIdentifier(byte[] socketId)
        {
            SocketId = socketId;
        }

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
            return (SocketId != null ? SocketId.GetHashCode() : 0);
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
            return Unsafe.Equals(SocketId, other.SocketId);
        }

        internal byte[] SocketId { get; }
    }
}