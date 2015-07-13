using System;

namespace rawf.Framework
{
    public class DuplicatedKeyException : Exception
    {
        public DuplicatedKeyException(string message)
            : base(message)
        {
        }
    }
}