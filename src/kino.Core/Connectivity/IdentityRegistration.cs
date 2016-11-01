using System;

namespace kino.Core.Connectivity
{
    public class IdentityRegistration : IEquatable<IdentityRegistration>
    {
        private readonly int hashCode;

        public IdentityRegistration(Identifier identifier)
            : this(identifier, false)
        {
        }

        public IdentityRegistration(Identifier identifier, bool localRegistration)
        {
            Identifier = identifier;
            LocalRegistration = localRegistration;

            hashCode = CalculateHashCode();
        }

        public bool Equals(IdentityRegistration other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Identifier.Equals(other.Identifier);
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
            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((IdentityRegistration) obj);
        }

        public override int GetHashCode()
            => hashCode;

        private int CalculateHashCode()
            => Identifier.GetHashCode();

        public static bool operator ==(IdentityRegistration left, IdentityRegistration right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(IdentityRegistration left, IdentityRegistration right)
        {
            return !Equals(left, right);
        }

        public Identifier Identifier { get; }

        public bool LocalRegistration { get; }
    }
}