﻿namespace kino.Client
{
    public class CallbackHandlerKey
    {
        public ushort Version { get; set; }

        public byte[] Identity { get; set; }

        public byte[] Partition { get; set; }

        public long CallbackKey { get; set; }
    }
}