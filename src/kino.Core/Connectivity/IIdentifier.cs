namespace kino.Core.Connectivity
{
    public interface IIdentifier
    {
        ushort Version { get; }

        byte[] Identity { get; }

        byte[] Partition { get; }
    }
}