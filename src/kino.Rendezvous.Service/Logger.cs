using NLog;
using ILogger = kino.Core.Diagnostics.ILogger;

namespace kino.Rendezvous.Service
{
    public class Logger : ILogger
    {
        private readonly NLog.Logger logger;

        public Logger(string name)
        {
            var config = new NLog.Config.XmlLoggingConfiguration("config//NLog.config");
            LogManager.Configuration = config;
            logger = LogManager.GetLogger(name);
        }

        public void Warn(object message)
            => logger.Warn(message);

        public void Info(object message)
            => logger.Info(message);

        public void Debug(object message)
            => logger.Debug(message);

        public void Error(object message)
            => logger.Error(message);

        public void Trace(object message)
            => logger.Trace(message);
    }
}