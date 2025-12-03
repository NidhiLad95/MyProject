namespace GenxAi_Solutions_V1.Utils.Logging
{
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _basePath;
        private readonly string _logType;
        private readonly LogLevel _minLogLevel;

        public FileLoggerProvider(string basePath, string logType, LogLevel minLogLevel = LogLevel.Information)
        {
            _basePath = basePath;
            _logType = logType;
            _minLogLevel = minLogLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(categoryName, _basePath, _logType);
        }

        public void Dispose() { }
    }
}
