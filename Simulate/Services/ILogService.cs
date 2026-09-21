using System;
using System.Collections.Generic;
using Simulate.Models;

namespace Simulate.Services
{
    public interface ILogService
    {
        void Log(LogLevel level, string sourceModule, string message);
        void LogInfo(string sourceModule, string message);
        void LogWarning(string sourceModule, string message);
        void LogError(string sourceModule, string message);
        void Clear();
        IReadOnlyList<LogEntry> GetEntries();
        event EventHandler<LogEntry>? LogAdded;
        event EventHandler? Cleared;
    }
}
