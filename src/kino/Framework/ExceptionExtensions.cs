using System;

namespace kino.Framework
{
    public static class ExceptionExtensions
    {
        public static bool OperationCanceled(this Exception err)
            => err is OperationCanceledException;
    }
}