using System;
using kino.Core;

namespace kino.Rendezvous.Configuration
{
    public class PartnerRendezvousEndpoint : IEquatable<PartnerRendezvousEndpoint>
    {
        private readonly DynamicUri broadcast;

        public PartnerRendezvousEndpoint(string broadcastUri)
            => broadcast = new DynamicUri(broadcastUri);

        public void RefreshUri()
            => broadcast.Refresh();

        public bool Equals(PartnerRendezvousEndpoint other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Equals(broadcast, other.broadcast);
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
            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((PartnerRendezvousEndpoint) obj);
        }

        public static bool operator ==(PartnerRendezvousEndpoint left, PartnerRendezvousEndpoint right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(PartnerRendezvousEndpoint left, PartnerRendezvousEndpoint right)
            => !(left == right);

        public override int GetHashCode()
            => CalculateHashCode();

        private int CalculateHashCode()
        {
            unchecked
            {
                return broadcast?.GetHashCode() ?? 0;
            }
        }

        public string BroadcastUri => broadcast.Uri;
    }
}