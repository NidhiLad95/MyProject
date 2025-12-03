using GenxAi_Solutions.Utils.Logging;

namespace GenxAi_Solutions.Utils
{
    public static class LoggerExtensions
    {
        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string basePath, string logType, LogLevel minLogLevel = LogLevel.Information)
        {
            builder.AddProvider(new FileLoggerProvider(basePath, logType, minLogLevel));
            return builder;
        }
    }
}
