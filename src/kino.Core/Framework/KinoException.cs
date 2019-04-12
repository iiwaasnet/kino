using System;

namespace kino.Core.Framework
{
    public class KinoException : Exception
    {
        public KinoException(string message, string type, string stackTrace)
            : base(message)
        {
            StackTrace = stackTrace;
            Type = type;
        }

        public override string ToString()
            => $"{Message} [{Type}] {StackTrace}";

        public override string StackTrace { get; }

        public string Type { get; }
    }
}