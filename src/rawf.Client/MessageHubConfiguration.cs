using System;

namespace rawf.Client
{
    public class MessageHubConfiguration : IMessageHubConfiguration
    {
        public Uri RouterUri { get; set; }
    }
}