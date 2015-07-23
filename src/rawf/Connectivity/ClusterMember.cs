using System;

namespace rawf.Connectivity
{
    public class ClusterMember
    {
        public Uri Address { get; set; }
        public byte[] Identity { get; set; }
    }
}