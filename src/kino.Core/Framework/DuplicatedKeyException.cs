using System;

namespace kino.Core.Framework
{
    public class DuplicatedKeyException : Exception
    {
        public DuplicatedKeyException(string message)
            : base(message)
        {
        }
    }
}