﻿namespace Console
{
    internal class CallbackIdentifier : MessageHandlerIdentifier
    {
        public CallbackIdentifier(byte[] version, byte[] receiverIdentity)
            : base(version, receiverIdentity)
        {
        }
    }
}