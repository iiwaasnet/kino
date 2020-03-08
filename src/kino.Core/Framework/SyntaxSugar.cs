﻿using System;
using System.Runtime.CompilerServices;
using kino.Core.Diagnostics;

namespace kino.Core.Framework
{
    public static class SyntaxSugar
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T As<T>(this object value)
            where T : class
            => value as T;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Cast<T>(this object value)
            where T : class
            => (T) value;

        public static void SafeExecuteUntilCanceled(this Action wrappedMethod, ILogger logger)
        {
            try
            {
                wrappedMethod();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        public static void SafeExecute(this Action wrappedMethod, ILogger logger)
        {
            try
            {
                wrappedMethod();
            }

            catch (Exception err)
            {
                logger.Error(err);
            }
        }
    }
}