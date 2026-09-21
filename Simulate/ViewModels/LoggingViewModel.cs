using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.ViewModels
{
    public partial class LoggingViewModel : ObservableObject
    {
        private readonly ILogService _logService;
        private readonly IFileDialogService _fileDialogService;

        public ObservableCollection<string> AvailableLevels { get; } = new()
        {
            "All Levels",
            "Info",
            "Warning",
            "Error"
        };

        [ObservableProperty]
        private string _selectedLevel = "All Levels";

        [ObservableProperty]
        private string _formattedLogText = string.Empty;

        public LoggingViewModel()
            : this(new LogService(), new DefaultFileDialogService())
        {
        }

        public LoggingViewModel(ILogService logService, IFileDialogService fileDialogService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));

            _logService.LogAdded += OnLogAdded;
            _logService.Cleared += OnCleared;

            RebuildLogText();
        }

        public ILogService LogService => _logService;

        partial void OnSelectedLevelChanged(string value)
        {
            RebuildLogText();
        }

        [RelayCommand]
        public void Clear()
        {
            _logService.Clear();
            _logService.LogInfo("Log", "Log cleared by user.");
        }

        [RelayCommand]
        public async Task ExportAsync()
        {
            string? filePath = _fileDialogService.SaveFileDialog(
                "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                "Export Log",
                $"AFI_Log_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                await File.WriteAllTextAsync(filePath, FormattedLogText, Encoding.UTF8);
                _logService.LogInfo("Log", $"Exported log to '{filePath}'.");
            }
            catch (Exception ex)
            {
                _logService.LogError("Log", $"Failed to export log: {ex.Message}");
            }
        }

        private void OnLogAdded(object? sender, LogEntry entry)
        {
            if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(() => HandleLogAdded(entry));
            }
            else
            {
                HandleLogAdded(entry);
            }
        }

        private void HandleLogAdded(LogEntry entry)
        {
            if (!MatchesFilter(entry, SelectedLevel))
            {
                return;
            }

            if (string.IsNullOrEmpty(FormattedLogText))
            {
                FormattedLogText = entry.FormattedLine;
            }
            else
            {
                FormattedLogText += Environment.NewLine + entry.FormattedLine;
            }
        }

        private void OnCleared(object? sender, EventArgs e)
        {
            if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(() => FormattedLogText = string.Empty);
            }
            else
            {
                FormattedLogText = string.Empty;
            }
        }

        public void RebuildLogText()
        {
            IReadOnlyList<LogEntry> entries = _logService.GetEntries();
            var filtered = entries.Where(e => MatchesFilter(e, SelectedLevel)).Select(e => e.FormattedLine);
            FormattedLogText = string.Join(Environment.NewLine, filtered);
        }

        private static bool MatchesFilter(LogEntry entry, string filter)
        {
            if (string.Equals(filter, "All Levels", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(entry.Level.ToString(), filter, StringComparison.OrdinalIgnoreCase);
        }
    }
}
