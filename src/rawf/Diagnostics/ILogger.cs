namespace rawf.Diagnostics
{
    public interface ILogger
    {
        void Warn(object message);
        void Info(object message);
        void Debug(object message);
        void Error(object message);
    }
}