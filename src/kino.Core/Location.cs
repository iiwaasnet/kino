using System;
using kino.Core.Framework;

namespace kino.Core
{
    public class Location : IEquatable<Location>
    {
        private readonly string originalUri;
        private int hashCode;

        public Location(string uri)
        {
            originalUri = uri;
            RefreshLocation();
        }

        public void RefreshLocation()
        {
            Uri = originalUri.ParseAddress();
            hashCode = CalcHashCode();
        }

        public bool Equals(Location other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Equals(Uri, other.Uri);
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

            return Equals((Location) obj);
        }

        public override int GetHashCode()
            => hashCode;

        private int CalcHashCode()
            => Uri != null
                   ? Uri.GetHashCode()
                   : 0;

        public static bool operator ==(Location left, Location right)
            => Equals(left, right);

        public static bool operator !=(Location left, Location right)
            => !Equals(left, right);

        public Uri Uri { get; private set; }
    }
}