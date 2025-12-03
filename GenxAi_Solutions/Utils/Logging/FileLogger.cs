//using Microsoft.Extensions.Logging;
//using System.Text;

//namespace GenxAi_Solutions.Utils.Logging
//{
//    public class FileLogger : ILogger
//    {
//        private readonly string _categoryName;
//        private readonly string _basePath;
//        private readonly string _logType;
//        private readonly object _lock = new object();

//        public FileLogger(string categoryName, string basePath, string logType)
//        {
//            _categoryName = categoryName;
//            _basePath = basePath;
//            _logType = logType;

//            // Ensure directory exists
//            Directory.CreateDirectory(basePath);
//        }

//        public IDisposable BeginScope<TState>(TState state) => null;

//        public bool IsEnabled(LogLevel logLevel)
//        {
//            // For audit logger, only log information level and above
//            if (_logType == "Audit")
//                return logLevel >= LogLevel.Information;

//            return true;
//        }

//        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
//        {
//            if (!IsEnabled(logLevel))
//                return;

//            var message = formatter(state, exception);
//            if (string.IsNullOrEmpty(message) && exception == null)
//                return;

//            WriteToFile(logLevel, message, exception, eventId);
//        }

//        private void WriteToFile(LogLevel logLevel, string message, Exception exception, EventId eventId)
//        {
//            string fileName = GetFileName(logLevel);
//            var filePath = Path.Combine(_basePath, fileName);

//            var logEntry = new StringBuilder();

//            // Different format for audit logs
//            if (_logType == "Audit")
//            {
//                logEntry.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [AUDIT]");
//                logEntry.AppendLine($"Event: {eventId.Name ?? "General Audit"}");
//                logEntry.AppendLine($"Details: {message}");
//                logEntry.AppendLine(new string('-', 60));
//            }
//            else if (_logType == "Error" || logLevel >= LogLevel.Error)
//            {
//                logEntry.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}]");
//                logEntry.AppendLine($"Category: {_categoryName}");

//                if (!string.IsNullOrEmpty(message))
//                {
//                    logEntry.AppendLine($"Message: {message}");
//                }

//                if (exception != null)
//                {
//                    logEntry.AppendLine($"Exception Type: {exception.GetType().FullName}");
//                    logEntry.AppendLine($"Exception Message: {exception.Message}");
//                    logEntry.AppendLine($"Stack Trace: {exception.StackTrace}");

//                    // Include file and line number information if available
//                    var stackTrace = new System.Diagnostics.StackTrace(exception, true);
//                    var frame = stackTrace.GetFrame(0);
//                    if (frame != null)
//                    {
//                        var fileNameStack = frame.GetFileName();
//                        var lineNumber = frame.GetFileLineNumber();
//                        if (!string.IsNullOrEmpty(fileNameStack))
//                        {
//                            logEntry.AppendLine($"File: {Path.GetFileName(fileNameStack)}");
//                            logEntry.AppendLine($"Line: {lineNumber}");
//                        }
//                    }

//                    // Inner exception
//                    if (exception.InnerException != null)
//                    {
//                        logEntry.AppendLine($"Inner Exception: {exception.InnerException}");
//                    }
//                }
//                logEntry.AppendLine(new string('-', 80));
//            }
//            else
//            {
//                // Information logs
//                logEntry.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}]");
//                logEntry.AppendLine($"Category: {_categoryName}");
//                logEntry.AppendLine($"Message: {message}");
//                logEntry.AppendLine(new string('-', 60));
//            }

//            lock (_lock)
//            {
//                File.AppendAllText(filePath, logEntry.ToString());
//            }
//        }

//        private string GetFileName(LogLevel logLevel)
//        {
//            return _logType switch
//            {
//                "Audit" => $"audit_{DateTime.Now:yyyyMMdd}.txt",
//                "Error" when logLevel >= LogLevel.Error => $"errors_{DateTime.Now:yyyyMMdd}.txt",
//                "Error" => $"information_{DateTime.Now:yyyyMMdd}.txt",
//                "Information" when logLevel >= LogLevel.Error => $"errors_{DateTime.Now:yyyyMMdd}.txt",
//                "Information" => $"information_{DateTime.Now:yyyyMMdd}.txt",
//                _ => $"general_{DateTime.Now:yyyyMMdd}.txt"
//            };
//        }
//    }
//}


using Microsoft.Extensions.Logging;
using System.Text;

namespace GenxAi_Solutions.Utils.Logging
{
    public class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _basePath;
        private readonly string _logType;
        private readonly object _lock = new object();
        private static readonly Dictionary<string, object> _fileLocks = new Dictionary<string, object>();

        public FileLogger(string categoryName, string basePath, string logType)
        {
            _categoryName = categoryName;
            _basePath = basePath;
            _logType = logType;

            // Ensure directory exists
            Directory.CreateDirectory(basePath);
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            // For audit logger, only log information level and above
            if (_logType == "Audit")
                return logLevel >= LogLevel.Information;

            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception == null)
                return;

            WriteToFile(logLevel, message, exception, eventId);
        }

        private void WriteToFile(LogLevel logLevel, string message, Exception exception, EventId eventId)
        {
            string fileName = GetFileName(logLevel);
            var filePath = Path.Combine(_basePath, fileName);

            var logEntry = BuildLogEntry(logLevel, message, exception, eventId);

            // Get or create a lock object for this specific file
            object fileLock;
            lock (_fileLocks)
            {
                if (!_fileLocks.ContainsKey(filePath))
                {
                    _fileLocks[filePath] = new object();
                }
                fileLock = _fileLocks[filePath];
            }

            // Use the file-specific lock
            lock (fileLock)
            {
                try
                {
                    // Use FileStream with proper sharing mode
                    using (var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(stream, Encoding.UTF8))
                    {
                        writer.Write(logEntry.ToString());
                    }
                }
                catch (IOException ioEx)
                {
                    // If we still get IO exceptions, implement retry logic
                    Thread.Sleep(10); // Small delay before retry
                    try
                    {
                        using (var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                        using (var writer = new StreamWriter(stream, Encoding.UTF8))
                        {
                            writer.Write(logEntry.ToString());
                        }
                    }
                    catch
                    {
                        // If retry fails, log to a fallback location or ignore
                        System.Diagnostics.Debug.WriteLine($"Failed to write log to {filePath}: {ioEx.Message}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Unexpected error writing log: {ex.Message}");
                }
            }
        }

        private StringBuilder BuildLogEntry(LogLevel logLevel, string message, Exception exception, EventId eventId)
        {
            var logEntry = new StringBuilder();

            // Different format for audit logs
            if (_logType == "Audit")
            {
                logEntry.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [AUDIT]");
                logEntry.AppendLine($"Event: {eventId.Name ?? "General Audit"}");
                logEntry.AppendLine($"Details: {message}");
                logEntry.AppendLine(new string('-', 60));
            }
            else if (_logType == "Error" || logLevel >= LogLevel.Error)
            {
                logEntry.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}]");
                logEntry.AppendLine($"Category: {_categoryName}");

                if (!string.IsNullOrEmpty(message))
                {
                    logEntry.AppendLine($"Message: {message}");
                }

                if (exception != null)
                {
                    logEntry.AppendLine($"Exception Type: {exception.GetType().FullName}");
                    logEntry.AppendLine($"Exception Message: {exception.Message}");
                    logEntry.AppendLine($"Stack Trace: {exception.StackTrace}");

                    // Include file and line number information if available
                    var stackTrace = new System.Diagnostics.StackTrace(exception, true);
                    var frame = stackTrace.GetFrame(0);
                    if (frame != null)
                    {
                        var fileNameStack = frame.GetFileName();
                        var lineNumber = frame.GetFileLineNumber();
                        if (!string.IsNullOrEmpty(fileNameStack))
                        {
                            logEntry.AppendLine($"File: {Path.GetFileName(fileNameStack)}");
                            logEntry.AppendLine($"Line: {lineNumber}");
                        }
                    }

                    // Inner exception
                    if (exception.InnerException != null)
                    {
                        logEntry.AppendLine($"Inner Exception: {exception.InnerException}");
                    }
                }
                logEntry.AppendLine(new string('-', 80));
            }
            else
            {
                // Information logs
                logEntry.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}]");
                logEntry.AppendLine($"Category: {_categoryName}");
                logEntry.AppendLine($"Message: {message}");
                logEntry.AppendLine(new string('-', 60));
            }

            return logEntry;
        }

        private string GetFileName(LogLevel logLevel)
        {
            return _logType switch
            {
                "Audit" => $"audit_{DateTime.Now:yyyyMMdd}.txt",
                "Error" when logLevel >= LogLevel.Error => $"errors_{DateTime.Now:yyyyMMdd}.txt",
                "Error" => $"information_{DateTime.Now:yyyyMMdd}.txt",
                "Information" when logLevel >= LogLevel.Error => $"errors_{DateTime.Now:yyyyMMdd}.txt",
                "Information" => $"information_{DateTime.Now:yyyyMMdd}.txt",
                _ => $"general_{DateTime.Now:yyyyMMdd}.txt"
            };
        }
    }
}
