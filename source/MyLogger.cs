using System;
using Microsoft.Extensions.Logging;

namespace G3SDK
{
    public class MyLogger : ILogger
    {
        private readonly string _loggerName;
        private readonly Microsoft.Extensions.Logging.LogLevel _logLevel;

        public MyLogger(string loggerName, Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            _loggerName = loggerName;
            _logLevel = logLevel;
        }

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel >= _logLevel)
                LogHelper.LogMsg($"{_loggerName}: [{eventId.Id}] [{eventId.Name}] {formatter(state, exception)}");
        }

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return new ScopeToken();
        }

        internal class ScopeToken : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}