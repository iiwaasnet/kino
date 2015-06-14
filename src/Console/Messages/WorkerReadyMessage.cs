using System.Collections.Generic;

namespace Console.Messages
{
    public class WorkerReadyMessage : TypedMessage<WorkerReadyMessage.Payload>
    {
        public const string MessageType = "WRKRDY";

        public WorkerReadyMessage(Payload payload)
            : base(payload, MessageType)
        {
        }

        public class Payload
        {
            public IEnumerable<string> IncomeMessages { get; set; }
        }
    }
}