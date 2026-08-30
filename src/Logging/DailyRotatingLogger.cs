using System;
using System.IO;
using System.Text.RegularExpressions;
using MaxwellBoost.Config;

namespace MaxwellBoost.Logging
{
    public enum LogLevel
    {
        Info,
        Warn,
        Error,
        Debug
    }

    public class DailyRotatingLogger
    {
        private static readonly object _lock = new();
        private readonly AppSettings _settings;
        private DateTime _currentLogDate;

        public event Action<LogLevel, string>? OnLogMessage;

        public DailyRotatingLogger(AppSettings settings)
        {
            _settings = settings;
            _currentLogDate = DateTime.Today;
            EnsureDirectoryAndRotate();
        }

        public string LogFilePath => Path.Combine(_settings.LogDirectory, _settings.LogFileName);

        public void Info(string message) => Log(LogLevel.Info, message);
        public void Warn(string message) => Log(LogLevel.Warn, message);
        public void Error(string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message} | Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}" : message;
            Log(LogLevel.Error, fullMessage);
        }
        public void Debug(string message) => Log(LogLevel.Debug, message);

        public void Log(LogLevel level, string message)
        {
            var now = DateTime.Now;
            var formattedLine = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] [{level.ToString().ToUpperInvariant().PadRight(5)}] {message}";

            lock (_lock)
            {
                try
                {
                    CheckDateRollover(now);
                    EnsureDirectory();

                    using var fs = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using var writer = new StreamWriter(fs);
                    writer.WriteLine(formattedLine);
                    writer.Flush();
                }
                catch (Exception ex)
                {
                    // Fallback to console if file write fails
                    Console.Error.WriteLine($"[LOGGER ERROR] Failed to write to {LogFilePath}: {ex.Message}");
                }
            }

            try
            {
                OnLogMessage?.Invoke(level, formattedLine);
            }
            catch
            {
                // Ignore listener errors
            }
        }

        private void EnsureDirectory()
        {
            if (!Directory.Exists(_settings.LogDirectory))
            {
                Directory.CreateDirectory(_settings.LogDirectory);
            }
        }

        private void EnsureDirectoryAndRotate()
        {
            lock (_lock)
            {
                try
                {
                    EnsureDirectory();
                    CheckDateRollover(DateTime.Now);
                    CleanupOldLogs();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[LOGGER INIT] Cleanup error: {ex.Message}");
                }
            }
        }

        private void CheckDateRollover(DateTime now)
        {
            if (now.Date == _currentLogDate)
            {
                return;
            }

            // Date has rolled over to a new day
            RotateCurrentLog();
            _currentLogDate = now.Date;
            CleanupOldLogs();
        }

        private void RotateCurrentLog()
        {
            try
            {
                if (File.Exists(LogFilePath))
                {
                    var fileInfo = new FileInfo(LogFilePath);
                    var fileDate = fileInfo.LastWriteTime.Date;
                    var dateStr = fileDate.ToString("yyyy-MM-dd");
                    var archiveFileName = $"maxwell-{dateStr}.log";
                    var archivePath = Path.Combine(_settings.LogDirectory, archiveFileName);

                    if (File.Exists(archivePath))
                    {
                        archiveFileName = $"maxwell-{dateStr}-{DateTime.Now:HHmmss}.log";
                        archivePath = Path.Combine(_settings.LogDirectory, archiveFileName);
                    }

                    File.Move(LogFilePath, archivePath);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[LOGGER ROTATION] Error rotating log: {ex.Message}");
            }
        }

        public void CleanupOldLogs()
        {
            try
            {
                if (!Directory.Exists(_settings.LogDirectory))
                {
                    return;
                }

                var cutoffDate = DateTime.Today.AddDays(-_settings.LogRetentionDays);
                var dirInfo = new DirectoryInfo(_settings.LogDirectory);

                // STRICT FILTER: Only match files named maxwell-*.log to protect existing logs in C:\logs
                var files = dirInfo.GetFiles("maxwell-*.log", SearchOption.TopDirectoryOnly);

                var dateRegex = new Regex(@"^maxwell-(\d{4}-\d{2}-\d{2})", RegexOptions.IgnoreCase);

                foreach (var file in files)
                {
                    try
                    {
                        var match = dateRegex.Match(file.Name);
                        if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var logDate))
                        {
                            if (logDate < cutoffDate)
                            {
                                file.Delete();
                            }
                        }
                        else if (file.LastWriteTime.Date < cutoffDate)
                        {
                            file.Delete();
                        }
                    }
                    catch
                    {
                        // Ignore individual file deletion errors (e.g. if locked)
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[LOGGER CLEANUP] Error cleaning old logs: {ex.Message}");
            }
        }
    }
}
