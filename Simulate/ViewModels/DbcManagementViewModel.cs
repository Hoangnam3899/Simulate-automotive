using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.ViewModels
{
    public partial class DbcManagementViewModel : ObservableObject
    {
        private const int MaxDbcFileSizeBytes = 50 * 1024 * 1024; // 50 MB safety bound
        private readonly IFileDialogService _fileDialogService;

        [ObservableProperty]
        private string? _loadedFileName;

        [ObservableProperty]
        private string? _loadedFilePath;

        [ObservableProperty]
        private bool _isDbcLoaded;

        [ObservableProperty]
        private int _messageCount;

        [ObservableProperty]
        private int _nodeCount;

        [ObservableProperty]
        private int _signalCount;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string? _lastErrorMessage;

        [ObservableProperty]
        private DbcDocument? _loadedDocument;

        public event EventHandler<DbcDocument>? DocumentLoaded;
        public event EventHandler? DocumentUnloaded;

        public string LoadedFileNameDisplay => string.IsNullOrWhiteSpace(LoadedFileName) ? "No DBC Loaded" : LoadedFileName;

        public Visibility CheckmarkVisibility => IsDbcLoaded ? Visibility.Visible : Visibility.Collapsed;

        public bool CanLoadDbc => !IsBusy;

        public bool CanUnloadDbc => !IsBusy && IsDbcLoaded;

        public DbcManagementViewModel()
            : this(new DefaultFileDialogService())
        {
        }

        public DbcManagementViewModel(IFileDialogService fileDialogService)
        {
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        }

        [RelayCommand(CanExecute = nameof(CanLoadDbc))]
        public async Task LoadDbcAsync()
        {
            string? filePath = _fileDialogService.OpenFileDialog(
                "DBC Files (*.dbc)|*.dbc|All Files (*.*)|*.*",
                "Select CAN DBC File");

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            await LoadDbcFileAsync(filePath);
        }

        [RelayCommand(CanExecute = nameof(CanUnloadDbc))]
        public void UnloadDbc()
        {
            ClearDbcState();
            DocumentUnloaded?.Invoke(this, EventArgs.Empty);
            NotifyStateChanged();
        }

        public async Task<bool> LoadDbcFileAsync(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            if (!File.Exists(filePath))
            {
                LastErrorMessage = $"File not found: {filePath}";
                return false;
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxDbcFileSizeBytes)
            {
                LastErrorMessage = $"DBC file exceeds maximum allowed size of {MaxDbcFileSizeBytes / (1024 * 1024)} MB.";
                return false;
            }

            IsBusy = true;
            NotifyStateChanged();

            try
            {
                string text = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                return LoadDbcText(Path.GetFileName(filePath), filePath, text);
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"Failed to read DBC file: {ex.Message}";
                return false;
            }
            finally
            {
                IsBusy = false;
                NotifyStateChanged();
            }
        }

        public bool LoadDbcText(string fileName, string? filePath, string dbcContent)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            ArgumentNullException.ThrowIfNull(dbcContent);

            DbcParseResult result = DbcParser.Parse(dbcContent);
            if (!result.IsSuccess || result.Document is null)
            {
                string issueDetails = string.Join(Environment.NewLine,
                    result.Issues.Select(i => $"Line {i.LineNumber}: {i.Message}"));
                LastErrorMessage = $"DBC parsing failed:{Environment.NewLine}{issueDetails}";
                return false;
            }

            LoadedDocument = result.Document;
            LoadedFileName = fileName;
            LoadedFilePath = filePath;
            MessageCount = result.Document.Messages.Count;
            NodeCount = result.Document.Nodes.Count;
            SignalCount = result.Document.Messages.Sum(m => m.Signals.Count);
            IsDbcLoaded = true;
            LastErrorMessage = null;

            DocumentLoaded?.Invoke(this, result.Document);
            NotifyStateChanged();
            return true;
        }

        private void ClearDbcState()
        {
            LoadedDocument = null;
            LoadedFileName = null;
            LoadedFilePath = null;
            MessageCount = 0;
            NodeCount = 0;
            SignalCount = 0;
            IsDbcLoaded = false;
            LastErrorMessage = null;
        }

        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(LoadedFileNameDisplay));
            OnPropertyChanged(nameof(CheckmarkVisibility));
            OnPropertyChanged(nameof(CanLoadDbc));
            OnPropertyChanged(nameof(CanUnloadDbc));
            LoadDbcCommand.NotifyCanExecuteChanged();
            UnloadDbcCommand.NotifyCanExecuteChanged();
        }
    }
}
