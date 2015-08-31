using System;

namespace kino.Framework
{
    public class DuplicatedKeyException : Exception
    {
        public DuplicatedKeyException(string message)
            : base(message)
        {
        }
    }
}