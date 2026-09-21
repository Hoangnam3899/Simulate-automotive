using System;

namespace Simulate.Models
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    public sealed class LogEntry
    {
        public LogEntry(DateTimeOffset timestamp, LogLevel level, string sourceModule, string message)
        {
            Timestamp = timestamp;
            Level = level;
            SourceModule = string.IsNullOrWhiteSpace(sourceModule) ? "System" : sourceModule.Trim();
            Message = message ?? string.Empty;
            FormattedLine = $"[{Timestamp:HH:mm:ss.fff}]  [{Level.ToString().ToUpperInvariant(),-7}]  [{SourceModule}]  {Message}";
        }

        public DateTimeOffset Timestamp { get; }

        public LogLevel Level { get; }

        public string SourceModule { get; }

        public string Message { get; }

        public string FormattedLine { get; }

        public override string ToString() => FormattedLine;
    }
}
