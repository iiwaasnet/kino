namespace rawf.Diagnostics
{
    public interface ILogger
    {
        void Warn(object message);
        void WarnFormat(string format, params object[] param);

        void Info(object message);
        void InfoFormat(string format, params object[] param);

        void Debug(object message);
        void DebugFormat(string format, params object[] param);

        void Error(object message);
        void ErrorFormat(string format, params object[] param);
    }
}