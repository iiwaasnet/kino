using rawf.Framework;
using rawf.Messaging;

namespace rawf.Tests.Actor.Setup
{
    public class EmptyMessage : IPayload
    {
        public static readonly byte[] MessageIdentity = "EMPTY".GetBytes();
    }
}