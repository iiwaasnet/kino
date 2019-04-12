using System;
using System.Text;

namespace kino.Messaging.Messages
{
    public class ExceptionMessage : Payload
    {
        private static readonly IMessageSerializer MessageSerializer = new NewtonJsonMessageSerializer();
        private static readonly byte[] MessageIdentity = BuildFullIdentity("EXCEPTION");

        public ExceptionMessage()
            : base(MessageSerializer)
        {
        }

        public override byte[] Serialize()
            => MessageSerializer.Serialize(new ExceptionMessage
                                           {
                                               Exception = new Exception($"{Message} [{ExceptionType}] ==> {StackTrace}"),
                                               Message = Message,
                                               StackTrace = StackTrace,
                                               ExceptionType = ExceptionType
                                           });

        public override T Deserialize<T>(byte[] content)
        {
            try
            {
                return MessageSerializer.Deserialize<T>(content);
            }
            catch (Exception err)
            {
                return (T) (object) new ExceptionMessage
                                    {
                                        Exception = CreateSubstitutionException(err, content),
                                        Message = err.Message,
                                        ExceptionType = err.GetType().ToString()
                                    };
            }
        }

        private Exception CreateSubstitutionException(Exception serializationException, byte[] content)
            => new Exception($"This is the substitution {nameof(ExceptionMessage)} generated due to deserialization error: "
                             + $"==> {serializationException} <== "
                             + $"Original message content: ==> {Encoding.Default.GetString(content)} <==");

        [Obsolete("Subject to removal")]
        public Exception Exception { get; private set; }

        public string StackTrace { get; set; }

        public string Message { get; set; }

        public string ExceptionType { get; set; }

        public override ushort Version => Messaging.Message.CurrentVersion;

        public override byte[] Identity => MessageIdentity;
    }
}