using System;
using kino.Core.Framework;

namespace kino.Core
{
    public class DynamicUri : IEquatable<DynamicUri>
    {
        private readonly string originalUri;
        private int hashCode;

        public DynamicUri(string uri)
        {
            originalUri = uri;
            Refresh();
        }

        public void Refresh()
        {
            Uri = originalUri.ParseAddress();
            hashCode = CalcHashCode();
        }

        public bool Equals(DynamicUri other)
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

            return Equals((DynamicUri) obj);
        }

        public static bool operator ==(DynamicUri left, DynamicUri right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(DynamicUri left, DynamicUri right)
            => !(left == right);

        public override int GetHashCode()
            => hashCode;

        private int CalcHashCode()
            => Uri != null
                   ? Uri.GetHashCode()
                   : 0;

        public Uri Uri { get; private set; }
    }
}