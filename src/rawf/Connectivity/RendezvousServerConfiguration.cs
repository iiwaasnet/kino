using System;

namespace rawf.Connectivity
{
  public class RendezvousServerConfiguration : IEquatable<RendezvousServerConfiguration>
  {
    public bool Equals(RendezvousServerConfiguration other)
    {
      if (ReferenceEquals(null, other))
      {
        return false;
      }
      if (ReferenceEquals(this, other))
      {
        return true;
      }

      return Equals(MulticastUri, other.MulticastUri) && Equals(UnicastUri, other.UnicastUri);
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

      return Equals((RendezvousServerConfiguration) obj);
    }

    public override int GetHashCode()
    {
      unchecked
      {
        return ((MulticastUri?.GetHashCode() ?? 0)*397) ^ (UnicastUri?.GetHashCode() ?? 0);
      }
    }

    public Uri MulticastUri { get; set; }
    public Uri UnicastUri { get; set; }
  }
}