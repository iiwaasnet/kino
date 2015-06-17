using System.Collections.Generic;

namespace Console.Messages
{
    public class WorkerReadyMessage : TypedMessage<WorkerReadyMessage.Payload>
    {
        public const string MessageIdentity = "WRKRDY";

        public WorkerReadyMessage(Payload payload)
            : base(payload, MessageIdentity)
        {
        }

        public class Payload
        {
            public IEnumerable<string> IncomeMessages { get; set; }
        }
    }
}