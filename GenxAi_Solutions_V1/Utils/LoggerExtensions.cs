using GenxAi_Solutions_V1.Utils.Logging;

namespace GenxAi_Solutions_V1.Utils
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
