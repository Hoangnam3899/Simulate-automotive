using System;
using System.Collections.Generic;
using Simulate.Models;

namespace Simulate.Services
{
    public sealed class LogService : ILogService
    {
        private readonly object _sync = new();
        private readonly List<LogEntry> _entries = new();
        private readonly int _maxCapacity;

        public LogService(int maxCapacity = 1000)
        {
            if (maxCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCapacity), "Max capacity must be greater than zero.");
            }

            _maxCapacity = maxCapacity;
        }

        public event EventHandler<LogEntry>? LogAdded;

        public event EventHandler? Cleared;

        public void Log(LogLevel level, string sourceModule, string message)
        {
            var entry = new LogEntry(DateTimeOffset.Now, level, sourceModule, message);

            lock (_sync)
            {
                if (_entries.Count >= _maxCapacity)
                {
                    _entries.RemoveAt(0);
                }

                _entries.Add(entry);
            }

            LogAdded?.Invoke(this, entry);
        }

        public void LogInfo(string sourceModule, string message) => Log(LogLevel.Info, sourceModule, message);

        public void LogWarning(string sourceModule, string message) => Log(LogLevel.Warning, sourceModule, message);

        public void LogError(string sourceModule, string message) => Log(LogLevel.Error, sourceModule, message);

        public void Clear()
        {
            lock (_sync)
            {
                _entries.Clear();
            }

            Cleared?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<LogEntry> GetEntries()
        {
            lock (_sync)
            {
                return _entries.ToArray();
            }
        }
    }
}
