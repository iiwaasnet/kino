using System.Collections.Generic;

namespace Console.Messages
{
    public class WorkerReady : IPayload
    {
        public const string MessageIdentity = "WRKRDY";
        public IEnumerable<MessageIdentity> MessageIdentities { get; set; }
    }
}