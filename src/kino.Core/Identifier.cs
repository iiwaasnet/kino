using System;
using kino.Core.Framework;

namespace kino.Core
{
    public abstract class Identifier : IIdentifier, IEquatable<IIdentifier>
    {
        public abstract bool Equals(IIdentifier other);

        public override int GetHashCode()
        {
            throw new NotImplementedException();
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
            if (!(obj is IIdentifier))
            {
                return false;
            }
            return Equals((IIdentifier) obj);
        }

        public static bool operator ==(Identifier left, Identifier right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(Identifier left, Identifier right)
            => !(left == right);

        public override string ToString()
            => $"{nameof(Identity)}[{Identity?.GetAnyString()}]-" +
               $"{nameof(Version)}[{Version}]-" +
               $"{nameof(Partition)}[{Partition?.GetAnyString()}]";

        public byte[] Identity { get; protected set; }

        public ushort Version { get; protected set; }

        public byte[] Partition { get; protected set; }
    }
}