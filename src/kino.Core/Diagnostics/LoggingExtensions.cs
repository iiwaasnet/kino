using System;
using Microsoft.Extensions.Logging;

namespace kino.Core.Diagnostics
{
    public static class LoggingExtensions
    {
        public static void LogError(this ILogger logger, Exception err)
            => logger.LogError(err, null);

        public static void LogError(this ILogger logger, string message)
            => logger.LogError(message, null);
    }
}