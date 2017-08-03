using System;
using kino.Core;

namespace kino.Cluster.Configuration
{
    public class RendezvousEndpoint : IEquatable<RendezvousEndpoint>
    {
        private readonly DynamicUri unicast;
        private readonly DynamicUri broadcast;

        public RendezvousEndpoint(string unicastUri, string broadcastUri)
        {
            unicast = new DynamicUri(unicastUri);
            broadcast = new DynamicUri(broadcastUri);
        }

        public void RefreshUri()
        {
            broadcast.Refresh();
            unicast.Refresh();
        }

        public bool Equals(RendezvousEndpoint other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Equals(broadcast, other.broadcast) && Equals(unicast, other.unicast);
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

            return Equals((RendezvousEndpoint) obj);
        }

        public static bool operator ==(RendezvousEndpoint left, RendezvousEndpoint right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(RendezvousEndpoint left, RendezvousEndpoint right)
            => !(left == right);

        public override int GetHashCode()
            => CalculateHashCode();

        private int CalculateHashCode()
        {
            unchecked
            {
                return ((broadcast?.GetHashCode() ?? 0) * 397) ^ (unicast?.GetHashCode() ?? 0);
            }
        }

        public Uri BroadcastUri => broadcast.Uri;

        public Uri UnicastUri => unicast.Uri;
    }
}