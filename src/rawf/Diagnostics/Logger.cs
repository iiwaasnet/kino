namespace rawf.Diagnostics
{
    public class Logger : ILogger
    {
        private readonly NLog.Logger logger;

        public Logger(string name)
        {
            logger = NLog.LogManager.GetLogger(name);
        }

        public void Warn(object message)
        {
            logger.Warn(message);
        }

        public void WarnFormat(string format, params object[] param)
        {
            logger.Warn(format, param);
        }

        public void Info(object message)
        {
            logger.Info(message);
        }

        public void InfoFormat(string format, params object[] param)
        {
            logger.Info(format, param);
        }

        public void Debug(object message)
        {
            logger.Debug(message);
        }

        public void DebugFormat(string format, params object[] param)
        {
            logger.Debug(format, param);
        }

        public void Error(object message)
        {
            logger.Error(message);
        }

        public void ErrorFormat(string format, params object[] param)
        {
            logger.Error(format, param);
        }
    }
}