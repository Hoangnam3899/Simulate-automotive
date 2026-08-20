using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.ViewModels
{
    /// <summary>
    /// Projects one immutable simulation plan and its caller-owned runtime engine into typed binding data.
    /// </summary>
    public partial class SimulationViewModel : ObservableObject
    {
        private readonly ISimulationEngine? _engine;
        private readonly IMessageDialogService _messageDialogService;
        private DbcDocument? _currentDocument;

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private bool _isScheduling;

        [ObservableProperty]
        private bool _isSchedulingPaused;

        [ObservableProperty]
        private GatewayStatistics _statistics = GatewayStatistics.Empty;

        [ObservableProperty]
        private HardwareFailure? _lastFailure;

        [ObservableProperty]
        private MessageModel? _selectedMessage;

        [ObservableProperty]
        private string _signalSearchText = string.Empty;

        [ObservableProperty]
        private string _selectedSignalMessageFilter = "All Messages";

        [ObservableProperty]
        private bool _isSignalMonitorPaused;

        public ObservableCollection<string> AvailableSignalMessageFilters { get; } = new() { "All Messages" };

        public ICollectionView? FilteredSignals { get; }

        public string PauseMonitorButtonContent => IsSignalMonitorPaused ? "▶" : "Ⅱ";

        public bool CanAddMessages => _currentDocument is not null;
        public bool CanDeleteMessage => SelectedMessage is not null;
        public bool CanDeleteAllMessages => Messages.Count > 0;
        public bool CanMoveUp => SelectedMessage is not null && Messages.IndexOf(SelectedMessage) > 0;
        public bool CanMoveDown => SelectedMessage is not null && Messages.IndexOf(SelectedMessage) >= 0 && Messages.IndexOf(SelectedMessage) < Messages.Count - 1;

        /// <summary>
        /// Initializes an unconfigured projection for the UI before a DBC document and engine are composed.
        /// </summary>
        public SimulationViewModel()
            : this(new DefaultMessageDialogService())
        {
        }

        public SimulationViewModel(IMessageDialogService messageDialogService)
        {
            _messageDialogService = messageDialogService ?? throw new ArgumentNullException(nameof(messageDialogService));

            FilteredSignals = CollectionViewSource.GetDefaultView(Signals);
            if (FilteredSignals is not null)
            {
                FilteredSignals.Filter = FilterSignal;
            }
        }

        /// <summary>
        /// Initializes a projection from a validated plan and an already-created simulation engine.
        /// </summary>
        /// <param name="plan">The immutable DBC-bound simulation configuration to project.</param>
        /// <param name="engine">The caller-owned engine whose state is reflected without hardware access.</param>
        public SimulationViewModel(SimulationPlan plan, ISimulationEngine engine)
            : this(plan, engine, new DefaultMessageDialogService())
        {
        }

        public SimulationViewModel(SimulationPlan plan, ISimulationEngine engine, IMessageDialogService messageDialogService)
        {
            ArgumentNullException.ThrowIfNull(plan);
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _messageDialogService = messageDialogService ?? throw new ArgumentNullException(nameof(messageDialogService));

            FilteredSignals = CollectionViewSource.GetDefaultView(Signals);
            if (FilteredSignals is not null)
            {
                FilteredSignals.Filter = FilterSignal;
            }

            _currentDocument = plan.Document;
            ProjectPlan(plan);
            RefreshRuntimeState();
            NotifyToolbarCommands();
            UpdateAvailableSignalMessageFilters();
        }

        /// <summary>
        /// Gets the DBC message rows for the existing message-list binding.
        /// </summary>
        public ObservableCollection<MessageModel> Messages { get; } = new();

        /// <summary>
        /// Gets the DBC signal rows for the existing signal-list bindings.
        /// </summary>
        public ObservableCollection<SignalModel> Signals { get; } = new();

        /// <summary>
        /// Gets the configured signal overrides projected as fault-queue rows.
        /// </summary>
        public ObservableCollection<FaultQueueModel> FaultQueue { get; } = new();

        /// <summary>
        /// Gets a value indicating whether this instance has an engine supplied at the composition boundary.
        /// </summary>
        public bool IsConfigured => _engine is not null;

        partial void OnSelectedMessageChanged(MessageModel? value)
        {
            NotifyToolbarCommands();
        }

        partial void OnSignalSearchTextChanged(string value)
        {
            FilteredSignals?.Refresh();
        }

        partial void OnSelectedSignalMessageFilterChanged(string value)
        {
            FilteredSignals?.Refresh();
        }

        partial void OnIsSignalMonitorPausedChanged(bool value)
        {
            OnPropertyChanged(nameof(PauseMonitorButtonContent));
        }

        [RelayCommand]
        public void TogglePauseMonitor()
        {
            IsSignalMonitorPaused = !IsSignalMonitorPaused;
        }

        [RelayCommand]
        public void ClearSignalMonitor()
        {
            foreach (var signal in Signals)
            {
                signal.ResetData();
            }
        }

        private bool FilterSignal(object item)
        {
            if (item is not SignalModel signal)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(SelectedSignalMessageFilter) &&
                !string.Equals(SelectedSignalMessageFilter, "All Messages", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(signal.MessageName, SelectedSignalMessageFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(SignalSearchText))
            {
                string search = SignalSearchText.Trim();
                bool matchesName = signal.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
                bool matchesMsgName = signal.MessageName.Contains(search, StringComparison.OrdinalIgnoreCase);
                bool matchesMsgId = signal.MessageId.Contains(search, StringComparison.OrdinalIgnoreCase);
                if (!matchesName && !matchesMsgName && !matchesMsgId)
                {
                    return false;
                }
            }

            return true;
        }

        public void ProcessIncomingFrame(uint identifier, bool isExtended, ReadOnlySpan<byte> payload, DateTime? timestamp = null)
        {
            if (IsSignalMonitorPaused)
            {
                return;
            }

            string messageId = FormatIdentifier(identifier, isExtended);
            DateTime time = timestamp ?? DateTime.Now;

            foreach (var signal in Signals)
            {
                if (string.Equals(signal.MessageId, messageId, StringComparison.OrdinalIgnoreCase) && signal.DbcSource is not null)
                {
                    try
                    {
                        (ulong raw, double physical) = SignalCodec.Unpack(payload, signal.DbcSource);
                        signal.UpdateValue(raw, physical, time);
                    }
                    catch
                    {
                        // Payload may be shorter or malformed for this signal layout
                    }
                }
            }
        }

        private void UpdateAvailableSignalMessageFilters()
        {
            var distinctMessages = Signals
                .Select(s => s.MessageName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            string currentSelection = SelectedSignalMessageFilter;

            AvailableSignalMessageFilters.Clear();
            AvailableSignalMessageFilters.Add("All Messages");
            foreach (var msg in distinctMessages)
            {
                AvailableSignalMessageFilters.Add(msg);
            }

            if (AvailableSignalMessageFilters.Contains(currentSelection))
            {
                SelectedSignalMessageFilter = currentSelection;
            }
            else
            {
                SelectedSignalMessageFilter = "All Messages";
            }
        }

        [RelayCommand(CanExecute = nameof(CanAddMessages))]
        public void AddMessages()
        {
            if (_currentDocument is null)
            {
                return;
            }

            var alreadyAdded = Messages
                .Where(m => m.DbcSource is not null)
                .Select(m => m.DbcSource!)
                .ToList();

            IReadOnlyList<DbcMessage>? selected = _messageDialogService.SelectMessages(_currentDocument, alreadyAdded);
            if (selected is not null && selected.Count > 0)
            {
                AddMessages(selected);
            }
        }

        public void AddMessage(DbcMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);

            string messageId = FormatIdentifier(message.Identifier, message.IsExtendedIdentifier);
            var model = new MessageModel
            {
                DisplayIndex = Messages.Count + 1,
                Id = messageId,
                RawIdentifier = message.Identifier,
                IsExtendedIdentifier = message.IsExtendedIdentifier,
                Name = message.Name,
                Dlc = message.PayloadLength,
                Cycle = "100 ms",
                GatewayMode = "PassThrough",
                SendType = "Cyclic",
                SignalCount = message.Signals.Count,
                IsEnabled = true,
                LastSent = "—",
                DbcSource = message
            };

            Messages.Add(model);
            SelectedMessage = model;

            foreach (DbcSignal signal in message.Signals)
            {
                Signals.Add(new SignalModel
                {
                    Name = signal.Name,
                    StartBit = signal.StartBit,
                    Length = signal.BitLength,
                    Factor = signal.Factor,
                    Offset = signal.Offset,
                    Unit = signal.Unit,
                    Min = signal.Minimum,
                    Max = signal.Maximum,
                    Value = signal.Offset,
                    RawValue = "—",
                    PhysicalValueDisplay = "—",
                    MessageId = messageId,
                    MessageName = message.Name,
                    IsOverridden = false,
                    Cycle = model.Cycle,
                    HasReceivedData = false,
                    StatusText = "● No Data",
                    StatusColor = "#64748B",
                    LastUpdated = "—",
                    DbcSource = signal
                });
            }

            UpdateAvailableSignalMessageFilters();
            FilteredSignals?.Refresh();
            NotifyToolbarCommands();
        }

        public void AddMessages(IEnumerable<DbcMessage> messages)
        {
            ArgumentNullException.ThrowIfNull(messages);

            foreach (var message in messages)
            {
                bool exists = Messages.Any(m => m.RawIdentifier == message.Identifier && m.IsExtendedIdentifier == message.IsExtendedIdentifier);
                if (!exists)
                {
                    AddMessage(message);
                }
            }

            ReindexMessages();
            UpdateAvailableSignalMessageFilters();
            FilteredSignals?.Refresh();
            NotifyToolbarCommands();
        }

        [RelayCommand(CanExecute = nameof(CanDeleteMessage))]
        public void DeleteMessage()
        {
            if (SelectedMessage is null)
            {
                return;
            }

            var toRemove = SelectedMessage;
            int index = Messages.IndexOf(toRemove);

            var signalsToRemove = Signals.Where(s => s.MessageId == toRemove.Id).ToList();
            foreach (var sig in signalsToRemove)
            {
                Signals.Remove(sig);
            }

            Messages.Remove(toRemove);
            ReindexMessages();

            if (Messages.Count > 0)
            {
                int newIndex = Math.Clamp(index, 0, Messages.Count - 1);
                SelectedMessage = Messages[newIndex];
            }
            else
            {
                SelectedMessage = null;
            }

            UpdateAvailableSignalMessageFilters();
            FilteredSignals?.Refresh();
            NotifyToolbarCommands();
        }

        [RelayCommand(CanExecute = nameof(CanDeleteAllMessages))]
        public void DeleteAllMessages()
        {
            Messages.Clear();
            Signals.Clear();
            SelectedMessage = null;
            UpdateAvailableSignalMessageFilters();
            FilteredSignals?.Refresh();
            NotifyToolbarCommands();
        }

        [RelayCommand(CanExecute = nameof(CanMoveUp))]
        public void MoveUp()
        {
            if (SelectedMessage is null)
            {
                return;
            }

            int index = Messages.IndexOf(SelectedMessage);
            if (index > 0)
            {
                Messages.Move(index, index - 1);
                ReindexMessages();
                NotifyToolbarCommands();
            }
        }

        [RelayCommand(CanExecute = nameof(CanMoveDown))]
        public void MoveDown()
        {
            if (SelectedMessage is null)
            {
                return;
            }

            int index = Messages.IndexOf(SelectedMessage);
            if (index >= 0 && index < Messages.Count - 1)
            {
                Messages.Move(index, index + 1);
                ReindexMessages();
                NotifyToolbarCommands();
            }
        }

        private void ReindexMessages()
        {
            for (int i = 0; i < Messages.Count; i++)
            {
                Messages[i].DisplayIndex = i + 1;
            }
        }

        private void NotifyToolbarCommands()
        {
            OnPropertyChanged(nameof(CanAddMessages));
            OnPropertyChanged(nameof(CanDeleteMessage));
            OnPropertyChanged(nameof(CanDeleteAllMessages));
            OnPropertyChanged(nameof(CanMoveUp));
            OnPropertyChanged(nameof(CanMoveDown));
            AddMessagesCommand.NotifyCanExecuteChanged();
            DeleteMessageCommand.NotifyCanExecuteChanged();
            DeleteAllMessagesCommand.NotifyCanExecuteChanged();
            MoveUpCommand.NotifyCanExecuteChanged();
            MoveDownCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Sets the active DBC document without auto-populating TX messages.
        /// </summary>
        public void LoadDocument(DbcDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            _currentDocument = document;

            Messages.Clear();
            Signals.Clear();
            FaultQueue.Clear();
            SelectedMessage = null;

            UpdateAvailableSignalMessageFilters();
            FilteredSignals?.Refresh();
            NotifyToolbarCommands();
        }

        /// <summary>
        /// Clears message, signal, and fault queue projections when DBC is unloaded.
        /// </summary>
        public void ClearDocument()
        {
            _currentDocument = null;
            Messages.Clear();
            Signals.Clear();
            FaultQueue.Clear();
            SelectedMessage = null;

            UpdateAvailableSignalMessageFilters();
            FilteredSignals?.Refresh();
            NotifyToolbarCommands();
        }

        /// <summary>
        /// Refreshes observable runtime state without starting, stopping, or owning the engine.
        /// </summary>
        public void RefreshRuntimeState()
        {
            if (_engine is null)
            {
                IsRunning = false;
                IsScheduling = false;
                IsSchedulingPaused = false;
                Statistics = GatewayStatistics.Empty;
                LastFailure = null;
                return;
            }

            IsRunning = _engine.IsRunning;
            IsScheduling = _engine.IsScheduling;
            IsSchedulingPaused = _engine.IsSchedulingPaused;
            Statistics = _engine.Statistics;
            LastFailure = _engine.LastFailure;
        }

        /// <summary>
        /// Explicitly starts gateway routing through the configured engine.
        /// </summary>
        /// <param name="cancellationToken">Cancels the start operation before engine work begins.</param>
        /// <returns>A value task that completes after the engine accepted the start request.</returns>
        public async ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            await GetRequiredEngine().StartAsync(cancellationToken);
            RefreshRuntimeState();
        }

        /// <summary>
        /// Explicitly starts one-shot and cyclic scheduling through the configured engine.
        /// </summary>
        /// <param name="cancellationToken">Cancels the scheduling start request before engine work begins.</param>
        /// <returns>A value task that completes after scheduling has started.</returns>
        public async ValueTask StartSchedulingAsync(CancellationToken cancellationToken = default)
        {
            await GetRequiredEngine().StartSchedulingAsync(cancellationToken);
            RefreshRuntimeState();
        }

        /// <summary>
        /// Explicitly pauses scheduled sends while preserving gateway routing.
        /// </summary>
        public void PauseScheduling()
        {
            GetRequiredEngine().PauseScheduling();
            RefreshRuntimeState();
        }

        /// <summary>
        /// Explicitly resumes scheduled sends while preserving gateway routing.
        /// </summary>
        public void ResumeScheduling()
        {
            GetRequiredEngine().ResumeScheduling();
            RefreshRuntimeState();
        }

        /// <summary>
        /// Explicitly stops scheduled work while preserving gateway routing.
        /// </summary>
        /// <returns>A value task that completes after scheduled work has stopped.</returns>
        public async ValueTask StopSchedulingAsync()
        {
            try
            {
                await GetRequiredEngine().StopSchedulingAsync();
            }
            finally
            {
                RefreshRuntimeState();
            }
        }

        /// <summary>
        /// Explicitly requests an event-driven send and refreshes runtime state after it completes.
        /// </summary>
        /// <param name="canIdentifier">The normalized identifier of the configured event rule.</param>
        /// <param name="isExtendedIdentifier">Whether the configured event rule uses an extended identifier.</param>
        /// <param name="cancellationToken">Cancels the event trigger while it waits in the engine.</param>
        /// <returns>The typed engine outcome for the event trigger.</returns>
        public async ValueTask<SimulationEventTriggerResult> TriggerEventAsync(
            uint canIdentifier,
            bool isExtendedIdentifier,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await GetRequiredEngine().TriggerEventAsync(
                    canIdentifier,
                    isExtendedIdentifier,
                    cancellationToken);
            }
            finally
            {
                RefreshRuntimeState();
            }
        }

        /// <summary>
        /// Explicitly stops gateway routing and scheduled work through the configured engine.
        /// </summary>
        /// <returns>A value task that completes after engine work has stopped.</returns>
        public async ValueTask StopAsync()
        {
            try
            {
                await GetRequiredEngine().StopAsync();
            }
            finally
            {
                RefreshRuntimeState();
            }
        }

        /// <summary>
        /// Explicitly requests the engine's emergency stop while leaving session ownership with its caller.
        /// </summary>
        /// <returns>The typed cleanup result returned by the engine.</returns>
        public async ValueTask<HardwareOperationResult> EmergencyStopAsync()
        {
            try
            {
                return await GetRequiredEngine().EmergencyStopAsync();
            }
            finally
            {
                RefreshRuntimeState();
            }
        }

        private void ProjectPlan(SimulationPlan plan)
        {
            var rulesByMessage = plan.MessageRules.ToDictionary(
                rule => (rule.CanIdentifier, rule.IsExtendedIdentifier));
            var overridesBySignal = plan.MessageRules
                .SelectMany(rule => rule.SignalOverrides.Select(signalOverride => new
                {
                    rule.CanIdentifier,
                    rule.IsExtendedIdentifier,
                    signalOverride.SignalName,
                    signalOverride.PhysicalValue
                }))
                .ToDictionary(
                    item => (item.CanIdentifier, item.IsExtendedIdentifier, item.SignalName),
                    item => item.PhysicalValue);

            foreach (DbcMessage message in plan.Document.Messages)
            {
                rulesByMessage.TryGetValue(
                    (message.Identifier, message.IsExtendedIdentifier),
                    out SimulationMessageRule? rule);
                string messageId = FormatIdentifier(message.Identifier, message.IsExtendedIdentifier);
                Messages.Add(new MessageModel
                {
                    Id = messageId,
                    Name = message.Name,
                    Dlc = message.PayloadLength,
                    Cycle = FormatCycle(rule),
                    GatewayMode = rule?.GatewayMode.ToString() ?? "Unconfigured",
                    SendType = rule?.SendType.ToString() ?? "Unconfigured",
                    SignalCount = message.Signals.Count,
                    IsEnabled = rule?.IsEnabled ?? false
                });

                foreach (DbcSignal signal in message.Signals)
                {
                    bool isOverridden = overridesBySignal.TryGetValue(
                        (message.Identifier, message.IsExtendedIdentifier, signal.Name),
                        out double overrideValue);
                    Signals.Add(new SignalModel
                    {
                        Name = signal.Name,
                        StartBit = signal.StartBit,
                        Length = signal.BitLength,
                        Factor = signal.Factor,
                        Offset = signal.Offset,
                        Unit = signal.Unit,
                        Min = signal.Minimum,
                        Max = signal.Maximum,
                        Value = isOverridden ? overrideValue : signal.Offset,
                        MessageId = messageId,
                        MessageName = message.Name,
                        IsOverridden = isOverridden,
                        Cycle = FormatCycle(rule)
                    });
                }

                if (rule is not null)
                {
                    AddFaultQueueRows(message, messageId, rule);
                }
            }
        }

        private void AddFaultQueueRows(
            DbcMessage message,
            string messageId,
            SimulationMessageRule rule)
        {
            foreach (SignalOverride signalOverride in rule.SignalOverrides)
            {
                DbcSignal signal = message.Signals.Single(candidate =>
                    string.Equals(candidate.Name, signalOverride.SignalName, StringComparison.Ordinal));
                FaultQueue.Add(new FaultQueueModel
                {
                    Index = FaultQueue.Count + 1,
                    MsgId = messageId,
                    MsgName = message.Name,
                    Signal = signal.Name,
                    FaultType = "Signal override",
                    FaultValue = FormatPhysicalValue(signalOverride.PhysicalValue, signal.Unit),
                    StartTime = FormatDuration(rule.Timing.StartDelay),
                    Duration = "—",
                    Repeat = FormatRepeat(rule),
                    Mode = rule.GatewayMode.ToString(),
                    Status = rule.IsEnabled ? "Configured" : "Disabled",
                    StatusColor = rule.IsEnabled ? "#10B981" : "#94A3B8",
                    StatusBg = rule.IsEnabled ? "#064E3B" : "#1E2C3A"
                });
            }
        }

        private ISimulationEngine GetRequiredEngine()
        {
            return _engine ?? throw new InvalidOperationException(
                "A simulation plan and engine must be composed before runtime operations are requested.");
        }

        private static string FormatIdentifier(uint identifier, bool isExtendedIdentifier)
        {
            return isExtendedIdentifier
                ? $"0x{identifier:X8}"
                : $"0x{identifier:X3}";
        }

        private static string FormatCycle(SimulationMessageRule? rule)
        {
            if (rule is null)
            {
                return "—";
            }

            return rule.SendType switch
            {
                SimulationSendType.Cyclic => FormatDuration(rule.Timing.CycleInterval!.Value),
                SimulationSendType.OneShot => "One-shot",
                SimulationSendType.Event => "Event",
                _ => throw new InvalidOperationException("The simulation send type is not supported.")
            };
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalMilliseconds == 0d)
            {
                return "0 s";
            }

            if (duration.TotalMilliseconds < 1000d)
            {
                return duration.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) + " ms";
            }

            return duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " s";
        }

        private static string FormatPhysicalValue(double value, string unit)
        {
            string formattedValue = value.ToString("0.###", CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(unit) ? formattedValue : $"{formattedValue} {unit}";
        }

        private static string FormatRepeat(SimulationMessageRule rule)
        {
            return rule.SendType switch
            {
                SimulationSendType.Cyclic when rule.Timing.RepeatCount == 0 => "Infinite",
                SimulationSendType.Cyclic => rule.Timing.RepeatCount.ToString(CultureInfo.InvariantCulture),
                SimulationSendType.OneShot => "1",
                SimulationSendType.Event => "Event",
                _ => throw new InvalidOperationException("The simulation send type is not supported.")
            };
        }
    }
}
