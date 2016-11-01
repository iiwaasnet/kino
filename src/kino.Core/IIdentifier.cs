namespace kino.Core
{
    public interface IIdentifier
    {
        ushort Version { get; }

        byte[] Identity { get; }

        byte[] Partition { get; }
    }
}