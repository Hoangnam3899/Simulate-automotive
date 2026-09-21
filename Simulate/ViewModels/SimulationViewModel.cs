using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    public partial class SimulationViewModel : ObservableObject, IDisposable, IAsyncDisposable
    {
        private ISimulationEngine? _engine;
        private bool _ownsEngine;
        private readonly IMessageDialogService _messageDialogService;
        private DbcDocument? _currentDocument;
        private Func<ICanGatewaySession?>? _sessionProvider;
        private CancellationTokenSource? _executionCts;

        [ObservableProperty]
        private string _queueStatusText = "Idle";

        [ObservableProperty]
        private string _runningFaultDisplay = "—";

        [ObservableProperty]
        private string _pauseInjectionButtonContent = "Ⅱ  Pause";

        public string QueueItemsDisplay => FaultQueue.Count.ToString(CultureInfo.InvariantCulture);

        public ISimulationEngine? CurrentEngine => _engine;

        public void SetSessionProvider(Func<ICanGatewaySession?> sessionProvider)
        {
            _sessionProvider = sessionProvider;
            NotifyExecutionCommands();
        }

        public ICanGatewaySession? ActiveSession => _sessionProvider?.Invoke();

        public bool HasDocument => _currentDocument is not null;
        public DbcDocument? CurrentDocument => _currentDocument;

        public bool CanStartInjection => QueueStatusText == "Idle" &&
            (_engine is not null || ActiveSession is { IsOpen: true }) &&
            _currentDocument is not null;

        public bool CanStopInjection => QueueStatusText is "Running" or "Paused" || IsRunning || IsScheduling;

        public bool CanTogglePauseInjection => QueueStatusText is "Running" or "Paused";

        public bool CanClearQueue => FaultQueue.Count > 0 && QueueStatusText != "Running";

        partial void OnQueueStatusTextChanged(string value) => NotifyExecutionCommands();

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
        private SignalModel? _selectedSignal;

        [ObservableProperty]
        private string _signalSearchText = string.Empty;

        [ObservableProperty]
        private string _signalValueSearchText = string.Empty;

        [ObservableProperty]
        private bool _showOnlyOverridden;

        [ObservableProperty]
        private string _selectedSignalMessageFilter = "All Messages";

        [ObservableProperty]
        private bool _isSignalMonitorPaused;

        public ObservableCollection<string> AvailableSignalMessageFilters { get; } = new() { "All Messages" };

        public ICollectionView? FilteredSignals { get; }

        public ICollectionView? FilteredValueSignals { get; }

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

            FilteredValueSignals = new ListCollectionView(Signals);
            if (FilteredValueSignals is not null)
            {
                FilteredValueSignals.Filter = FilterValueSignal;
            }

            Signals.CollectionChanged += OnSignalsCollectionChanged;

            FaultConfig = new FaultConfigurationViewModel(FaultQueue);

            _ownsEngine = true;

            FaultQueue.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(QueueItemsDisplay));
                NotifyExecutionCommands();
            };
            NotifyExecutionCommands();
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

            FilteredValueSignals = new ListCollectionView(Signals);
            if (FilteredValueSignals is not null)
            {
                FilteredValueSignals.Filter = FilterValueSignal;
            }

            Signals.CollectionChanged += OnSignalsCollectionChanged;

            FaultConfig = new FaultConfigurationViewModel(FaultQueue);

            FaultQueue.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(QueueItemsDisplay));
                NotifyExecutionCommands();
            };

            _currentDocument = plan.Document;
            ProjectPlan(plan);
            RefreshRuntimeState();
            NotifyToolbarCommands();
            UpdateAvailableSignalMessageFilters();
            NotifyExecutionCommands();

            _engine.FrameRouted += OnEngineFrameRouted;
            EnsureLiveFlushTimerStarted();
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

        public FaultConfigurationViewModel FaultConfig { get; }

        /// <summary>
        /// Gets a value indicating whether this instance has an engine supplied at the composition boundary.
        /// </summary>
        public bool IsConfigured => _engine is not null;

        private bool _isSyncingMessageSelection;

        partial void OnSelectedMessageChanged(MessageModel? value)
        {
            NotifyToolbarCommands();
            if (!_isSyncingMessageSelection)
            {
                _isSyncingMessageSelection = true;
                try
                {
                    if (value is not null)
                    {
                        if (!AvailableSignalMessageFilters.Contains(value.Name))
                        {
                            AvailableSignalMessageFilters.Add(value.Name);
                        }
                        SelectedSignalMessageFilter = value.Name;
                    }
                    else
                    {
                        SelectedSignalMessageFilter = "All Messages";
                    }
                }
                finally
                {
                    _isSyncingMessageSelection = false;
                }
            }

            if (SelectedSignal is not null && value is not null &&
                !string.Equals(SelectedSignal.MessageId, value.Id, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(SelectedSignal.MessageName, value.Name, StringComparison.OrdinalIgnoreCase))
            {
                SelectedSignal = Signals.FirstOrDefault(s =>
                    string.Equals(s.MessageId, value.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.MessageName, value.Name, StringComparison.OrdinalIgnoreCase));
            }

            FilteredSignals?.Refresh();
            FilteredValueSignals?.Refresh();
        }

        partial void OnSelectedSignalChanged(SignalModel? value)
        {
            FaultConfig.SetTargetSignal(value);
        }

        partial void OnSignalSearchTextChanged(string value)
        {
            FilteredSignals?.Refresh();
        }

        partial void OnSelectedSignalMessageFilterChanged(string value)
        {
            if (!_isSyncingMessageSelection)
            {
                _isSyncingMessageSelection = true;
                try
                {
                    if (string.Equals(value, "All Messages", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(value))
                    {
                        SelectedMessage = null;
                    }
                    else
                    {
                        var matchingMsg = Messages.FirstOrDefault(m => string.Equals(m.Name, value, StringComparison.OrdinalIgnoreCase));
                        if (matchingMsg is not null)
                        {
                            SelectedMessage = matchingMsg;
                        }
                    }
                }
                finally
                {
                    _isSyncingMessageSelection = false;
                }
            }

            FilteredSignals?.Refresh();
            FilteredValueSignals?.Refresh();
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

            if (SelectedMessage is not null)
            {
                if (!string.Equals(signal.MessageId, SelectedMessage.Id, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(signal.MessageName, SelectedMessage.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(SelectedSignalMessageFilter) &&
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

        partial void OnSignalValueSearchTextChanged(string value)
        {
            FilteredValueSignals?.Refresh();
        }

        partial void OnShowOnlyOverriddenChanged(bool value)
        {
            FilteredValueSignals?.Refresh();
        }

        [RelayCommand]
        public void ClearAllOverrides()
        {
            foreach (var signal in Signals.Where(s => s.IsOverridden).ToList())
            {
                signal.IsOverridden = false;
            }
        }

        private void OnSignalsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (SignalModel item in e.NewItems)
                {
                    item.PropertyChanged += OnSignalItemPropertyChanged;
                }
            }

            if (e.OldItems is not null)
            {
                foreach (SignalModel item in e.OldItems)
                {
                    item.PropertyChanged -= OnSignalItemPropertyChanged;
                }
            }

            FilteredValueSignals?.Refresh();
        }

        private void OnSignalItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not SignalModel signal)
            {
                return;
            }

            if (e.PropertyName == nameof(SignalModel.IsOverridden) || (signal.IsOverridden && e.PropertyName == nameof(SignalModel.Value)))
            {
                if (ShowOnlyOverridden && e.PropertyName == nameof(SignalModel.IsOverridden))
                {
                    FilteredValueSignals?.Refresh();
                }

                SyncSignalOverrides(signal.MessageId);
            }
        }

        public void SyncSignalOverrides(string messageId)
        {
            if (_engine is null || string.IsNullOrEmpty(messageId))
            {
                return;
            }

            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message is null)
            {
                return;
            }

            var activeOverrides = Signals
                .Where(s => s.MessageId == messageId && s.IsOverridden)
                .Select(s => new SignalOverride(s.Name, s.Value))
                .ToList();

            // When injection is actively running, dynamically update the plan so any newly overridden
            // messages are promoted to Inject and applied immediately on the bus.
            if (QueueStatusText == "Running" && _engine.IsRunning && _currentDocument is not null)
            {
                try
                {
                    SimulationPlan plan = BuildSimulationPlan();
                    _engine.UpdatePlan(plan);
                    return;
                }
                catch
                {
                    // Fall back to ReplaceSignalOverrides if plan cannot be dynamically updated
                }
            }

            try
            {
                _engine.ReplaceSignalOverrides(
                    message.RawIdentifier,
                    message.IsExtendedIdentifier,
                    activeOverrides);
            }
            catch (ArgumentException)
            {
                // The engine is running in baseline pass-through mode without an inject rule for this message.
                // Overrides are safely maintained in the ViewModel and will be applied when Start Injection is triggered.
            }
        }

        private bool FilterValueSignal(object item)
        {
            if (item is not SignalModel signal)
            {
                return false;
            }

            if (SelectedMessage is not null)
            {
                if (!string.Equals(signal.MessageId, SelectedMessage.Id, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(signal.MessageName, SelectedMessage.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (ShowOnlyOverridden && !signal.IsOverridden)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(SignalValueSearchText))
            {
                string search = SignalValueSearchText.Trim();
                bool matchesName = signal.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
                bool matchesMsgName = signal.MessageName.Contains(search, StringComparison.OrdinalIgnoreCase);
                if (!matchesName && !matchesMsgName)
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
                bool matches = (signal.RawIdentifier != 0 && signal.RawIdentifier == identifier && signal.IsExtendedIdentifier == isExtended)
                    || string.Equals(signal.MessageId, messageId, StringComparison.OrdinalIgnoreCase);

                if (matches && signal.DbcSource is not null)
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

        private readonly object _liveBufferLock = new();
        private readonly Dictionary<(uint Id, bool IsExtended), byte[]> _liveFrameBuffer = new();
        private System.Threading.Timer? _liveFlushTimer;

        public void EnsureLiveFlushTimerStarted()
        {
            if (_liveFlushTimer is null)
            {
                _liveFlushTimer = new System.Threading.Timer(OnLiveFlushTimerTick, null, 33, 33);
            }
        }

        public void StopLiveFlushTimer()
        {
            _liveFlushTimer?.Dispose();
            _liveFlushTimer = null;
        }

        private void OnLiveFlushTimerTick(object? state)
        {
            if (IsSignalMonitorPaused)
            {
                return;
            }

            lock (_liveBufferLock)
            {
                if (_liveFrameBuffer.Count == 0)
                {
                    return;
                }
            }

            if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(FlushLiveBufferToSignals);
            }
            else
            {
                FlushLiveBufferToSignals();
            }
        }

        private void OnEngineFrameRouted(RoutedCanFrame routedFrame)
        {
            if (IsSignalMonitorPaused)
            {
                return;
            }

            lock (_liveBufferLock)
            {
                _liveFrameBuffer[(routedFrame.Frame.Identifier, routedFrame.Frame.IsExtendedIdentifier)] =
                    routedFrame.Frame.Data.ToArray();
            }

            EnsureLiveFlushTimerStarted();
        }

        public void FlushLiveBufferToSignals()
        {
            Dictionary<(uint Id, bool IsExtended), byte[]> snapshot;
            lock (_liveBufferLock)
            {
                if (_liveFrameBuffer.Count == 0)
                {
                    return;
                }
                snapshot = new Dictionary<(uint Id, bool IsExtended), byte[]>(_liveFrameBuffer);
                _liveFrameBuffer.Clear();
            }

            DateTime now = DateTime.Now;
            foreach (var kvp in snapshot)
            {
                ProcessIncomingFrame(kvp.Key.Id, kvp.Key.IsExtended, kvp.Value, now);
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
                    RawIdentifier = message.Identifier,
                    IsExtendedIdentifier = message.IsExtendedIdentifier,
                    IsOverridden = false,
                    Cycle = model.Cycle,
                    HasReceivedData = false,
                    StatusText = "● No Data",
                    StatusColor = "#64748B",
                    LastUpdated = "—",
                    DbcSource = signal
                });
            }

            if (_engine is { IsRunning: true })
            {
                _engine.UpdatePlan(BuildBaselineSimulationPlan());
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

            if (_engine is { IsRunning: true })
            {
                _engine.UpdatePlan(BuildBaselineSimulationPlan());
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

            if (_engine is { IsRunning: true })
            {
                _engine.UpdatePlan(BuildBaselineSimulationPlan());
            }

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

            if (_engine is not null && _engine.IsRunning)
            {
                SimulationPlan baselinePlan = BuildBaselineSimulationPlan();
                _engine.UpdatePlan(baselinePlan);
            }

            UpdateAvailableSignalMessageFilters();
            FilteredSignals?.Refresh();
            NotifyToolbarCommands();
            NotifyExecutionCommands();
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

            if (_engine is not null && _engine.IsRunning)
            {
                SimulationPlan rawPlan = SimulationPlan.CreateRawPassThrough();
                _engine.UpdatePlan(rawPlan);
            }

            UpdateAvailableSignalMessageFilters();
            FilteredSignals?.Refresh();
            NotifyToolbarCommands();
            NotifyExecutionCommands();
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

        public SimulationPlan BuildSimulationPlan()
        {
            if (_currentDocument is null)
            {
                throw new InvalidOperationException("A DBC document must be loaded to build a simulation plan.");
            }

            var rules = new List<SimulationMessageRule>();
            var messageKeysAdded = new HashSet<(uint, bool)>();

            if (Messages.Count > 0)
            {
                foreach (MessageModel msg in Messages)
                {
                    if (messageKeysAdded.Contains((msg.RawIdentifier, msg.IsExtendedIdentifier)))
                    {
                        continue;
                    }

                    DbcMessage? docMsg = _currentDocument.Messages.FirstOrDefault(m =>
                        m.Identifier == msg.RawIdentifier && m.IsExtendedIdentifier == msg.IsExtendedIdentifier);
                    if (docMsg is null)
                    {
                        continue;
                    }

                    var overrides = Signals
                        .Where(s => (s.MessageId == msg.Id || string.Equals(s.MessageName, msg.Name, StringComparison.OrdinalIgnoreCase)) && s.IsOverridden)
                        .Select(s =>
                        {
                            double safeVal = (s.Min < s.Max) ? Math.Clamp(s.Value, s.Min, s.Max) : s.Value;
                            return new SignalOverride(s.Name, safeVal);
                        })
                        .ToList();

                    bool isInject = overrides.Count > 0 || string.Equals(msg.GatewayMode, "Inject", StringComparison.OrdinalIgnoreCase);
                    GatewayMode gatewayMode = isInject
                        ? GatewayMode.Inject
                        : (string.Equals(msg.GatewayMode, "Block", StringComparison.OrdinalIgnoreCase) ? GatewayMode.Block : GatewayMode.PassThrough);

                    SimulationSendType sendType = SimulationSendType.Cyclic;
                    if (string.Equals(msg.SendType, "OneShot", StringComparison.OrdinalIgnoreCase) || string.Equals(msg.SendType, "One-Shot", StringComparison.OrdinalIgnoreCase))
                    {
                        sendType = SimulationSendType.OneShot;
                    }
                    else if (string.Equals(msg.SendType, "Event", StringComparison.OrdinalIgnoreCase))
                    {
                        sendType = SimulationSendType.Event;
                    }

                    TimeSpan cycle = FaultConfigurationViewModel.ParseTimeSpan(msg.Cycle, TimeSpan.FromMilliseconds(100));
                    if (cycle <= TimeSpan.Zero) cycle = TimeSpan.FromMilliseconds(100);

                    SimulationTiming timing = sendType switch
                    {
                        SimulationSendType.Cyclic => new SimulationTiming(TimeSpan.Zero, cycle, 0),
                        SimulationSendType.OneShot => new SimulationTiming(TimeSpan.Zero, null, 1),
                        SimulationSendType.Event => new SimulationTiming(TimeSpan.Zero, null, 0),
                        _ => new SimulationTiming(TimeSpan.Zero, cycle, 0)
                    };

                    rules.Add(new SimulationMessageRule(
                        msg.RawIdentifier,
                        msg.IsExtendedIdentifier,
                        msg.IsEnabled,
                        gatewayMode,
                        sendType,
                        timing,
                        overrides,
                        new E2eProtectionConfiguration(false)));

                    messageKeysAdded.Add((msg.RawIdentifier, msg.IsExtendedIdentifier));
                }
            }

            return new SimulationPlan(_currentDocument, rules);
        }

        public SimulationPlan BuildBaselineSimulationPlan()
        {
            if (_currentDocument is null)
            {
                return SimulationPlan.CreateRawPassThrough();
            }

            var rules = new List<SimulationMessageRule>();
            var messageKeysAdded = new HashSet<(uint, bool)>();

            if (Messages.Count > 0)
            {
                foreach (MessageModel msg in Messages)
                {
                    if (messageKeysAdded.Contains((msg.RawIdentifier, msg.IsExtendedIdentifier)))
                    {
                        continue;
                    }

                    DbcMessage? docMsg = _currentDocument.Messages.FirstOrDefault(m =>
                        m.Identifier == msg.RawIdentifier && m.IsExtendedIdentifier == msg.IsExtendedIdentifier);
                    if (docMsg is null)
                    {
                        continue;
                    }

                    TimeSpan cycle = FaultConfigurationViewModel.ParseTimeSpan(msg.Cycle, TimeSpan.FromMilliseconds(100));
                    if (cycle <= TimeSpan.Zero) cycle = TimeSpan.FromMilliseconds(100);

                    SimulationTiming timing = new SimulationTiming(TimeSpan.Zero, cycle, 0);

                    rules.Add(new SimulationMessageRule(
                        msg.RawIdentifier,
                        msg.IsExtendedIdentifier,
                        msg.IsEnabled,
                        GatewayMode.PassThrough,
                        SimulationSendType.Cyclic,
                        timing,
                        [],
                        new E2eProtectionConfiguration(false)));

                    messageKeysAdded.Add((msg.RawIdentifier, msg.IsExtendedIdentifier));
                }
            }
            else
            {
                foreach (DbcMessage msg in _currentDocument.Messages)
                {
                    if (messageKeysAdded.Contains((msg.Identifier, msg.IsExtendedIdentifier)))
                    {
                        continue;
                    }

                    TimeSpan cycle = TimeSpan.FromMilliseconds(100);
                    SimulationTiming timing = new SimulationTiming(TimeSpan.Zero, cycle, 0);

                    rules.Add(new SimulationMessageRule(
                        msg.Identifier,
                        msg.IsExtendedIdentifier,
                        true,
                        GatewayMode.PassThrough,
                        SimulationSendType.Cyclic,
                        timing,
                        [],
                        new E2eProtectionConfiguration(false)));

                    messageKeysAdded.Add((msg.Identifier, msg.IsExtendedIdentifier));
                }
            }

            return new SimulationPlan(_currentDocument, rules);
        }

        public async Task StartBaselineGatewayAsync()
        {
            if (_engine is { IsRunning: true })
            {
                return;
            }

            ICanGatewaySession? session = ActiveSession;
            if (session is not { IsOpen: true })
            {
                return;
            }

            try
            {
                SimulationPlan baselinePlan = BuildBaselineSimulationPlan();
                if (_engine is not null)
                {
                    _engine.FrameRouted -= OnEngineFrameRouted;
                    await _engine.DisposeAsync();
                }

                _engine = new SimulationEngine(session, baselinePlan);
                _engine.FrameRouted += OnEngineFrameRouted;
                EnsureLiveFlushTimerStarted();
                await _engine.StartAsync();
            }
            finally
            {
                RefreshRuntimeState();
                NotifyExecutionCommands();
            }
        }

        public async Task StopGatewayAsync()
        {
            try
            {
                _executionCts?.Cancel();

                if (_engine is not null)
                {
                    _engine.FrameRouted -= OnEngineFrameRouted;

                    if (_engine.IsScheduling)
                    {
                        await _engine.StopSchedulingAsync();
                    }

                    if (_engine.IsRunning)
                    {
                        await _engine.StopAsync();
                    }

                    await _engine.DisposeAsync();
                    _engine = null;
                }

                StopLiveFlushTimer();

                RestoreAllOverrides();

                foreach (var signal in Signals)
                {
                    signal.ResetData();
                }

                QueueStatusText = "Idle";
                RunningFaultDisplay = "—";
                PauseInjectionButtonContent = "Ⅱ  Pause";
            }
            finally
            {
                RefreshRuntimeState();
                NotifyExecutionCommands();
            }
        }

        [RelayCommand(CanExecute = nameof(CanStartInjection))]
        public async Task StartInjectionAsync()
        {
            if (!CanStartInjection)
            {
                return;
            }

            try
            {
                _executionCts?.Dispose();
                _executionCts = new CancellationTokenSource();
                CancellationToken token = _executionCts.Token;

                SimulationPlan plan = BuildSimulationPlan();

                if (_engine is null)
                {
                    ICanGatewaySession session = ActiveSession
                        ?? throw new InvalidOperationException("No active CAN gateway session is available.");
                    _engine = new SimulationEngine(session, plan);
                    _engine.FrameRouted += OnEngineFrameRouted;
                    EnsureLiveFlushTimerStarted();
                }
                else
                {
                    _engine.UpdatePlan(plan);
                }

                if (!_engine.IsRunning)
                {
                    await _engine.StartAsync(token);
                }

                if (!_engine.IsScheduling)
                {
                    try
                    {
                        await _engine.StartSchedulingAsync(token);
                    }
                    catch (InvalidOperationException)
                    {
                        // Engine may not have scheduled rules or may already be scheduling
                    }
                }

                QueueStatusText = "Running";
                PauseInjectionButtonContent = "Ⅱ  Pause";

                foreach (var signal in Signals.Where(s => s.IsOverridden))
                {
                    signal.RefreshOverriddenDisplay();
                }

                if (FaultQueue.Count > 0 && FaultConfig.SelectedInjectionMode == "Sequence")
                {
                    _ = RunSequenceQueueAsync(token);
                }
                else
                {
                    if (FaultConfig.TargetSignal is not null)
                    {
                        RunningFaultDisplay = $"{FaultConfig.TargetSignal.MessageName}.{FaultConfig.TargetSignal.Name}";
                    }
                    else
                    {
                        SignalModel? firstOverridden = Signals.FirstOrDefault(s => s.IsOverridden);
                        RunningFaultDisplay = firstOverridden is not null
                            ? $"{firstOverridden.MessageName}.{firstOverridden.Name}"
                            : "Active";
                    }

                    TimeSpan duration = FaultConfig.GetDuration();
                    if (duration > TimeSpan.Zero)
                    {
                        _ = RunDirectDurationAsync(duration, token);
                    }
                }
            }
            catch (Exception ex)
            {
                QueueStatusText = "Idle";
                RunningFaultDisplay = "—";
                if (ex is HardwareOperationException hwEx)
                {
                    LastFailure = hwEx.Failure;
                }
                throw;
            }
            finally
            {
                RefreshRuntimeState();
                NotifyExecutionCommands();
            }
        }

        [RelayCommand(CanExecute = nameof(CanStopInjection))]
        public async Task StopInjectionAsync()
        {
            try
            {
                _executionCts?.Cancel();

                if (_engine is not null)
                {
                    if (_engine.IsScheduling)
                    {
                        await _engine.StopSchedulingAsync();
                    }

                    if (FaultConfig.IsRestoreAfterStop)
                    {
                        RestoreAllOverrides();
                    }

                    if (_currentDocument is not null)
                    {
                        SimulationPlan baselinePlan = BuildBaselineSimulationPlan();
                        _engine.UpdatePlan(baselinePlan);
                    }
                }
                else if (FaultConfig.IsRestoreAfterStop)
                {
                    RestoreAllOverrides();
                }

                foreach (FaultQueueModel item in FaultQueue)
                {
                    if (item.Status == "Running")
                    {
                        item.Status = "Stopped";
                        item.StatusColor = "#EF4444";
                        item.StatusBg = "#7F1D1D";
                    }
                }

                QueueStatusText = "Idle";
                RunningFaultDisplay = "—";
                PauseInjectionButtonContent = "Ⅱ  Pause";
            }
            finally
            {
                RefreshRuntimeState();
                NotifyExecutionCommands();
            }
        }

        [RelayCommand(CanExecute = nameof(CanTogglePauseInjection))]
        public void TogglePauseInjection()
        {
            if (QueueStatusText == "Running")
            {
                if (_engine is not null && _engine.IsScheduling)
                {
                    _engine.PauseScheduling();
                }

                QueueStatusText = "Paused";
                PauseInjectionButtonContent = "▶  Resume";
            }
            else if (QueueStatusText == "Paused")
            {
                if (_engine is not null && _engine.IsSchedulingPaused)
                {
                    _engine.ResumeScheduling();
                }

                QueueStatusText = "Running";
                PauseInjectionButtonContent = "Ⅱ  Pause";
            }

            RefreshRuntimeState();
            NotifyExecutionCommands();
        }

        [RelayCommand(CanExecute = nameof(CanClearQueue))]
        public void ClearQueue()
        {
            FaultQueue.Clear();
            OnPropertyChanged(nameof(QueueItemsDisplay));
            NotifyExecutionCommands();
        }

        private void RestoreAllOverrides()
        {
            List<SignalModel> overriddenSignals = Signals.Where(s => s.IsOverridden).ToList();
            foreach (SignalModel sig in overriddenSignals)
            {
                sig.IsOverridden = false;
            }

            foreach (string msgId in overriddenSignals.Select(s => s.MessageId).Distinct())
            {
                SyncSignalOverrides(msgId);
            }
        }

        private async Task RunSequenceQueueAsync(CancellationToken token)
        {
            try
            {
                foreach (FaultQueueModel item in FaultQueue)
                {
                    if (token.IsCancellationRequested) break;

                    while (QueueStatusText == "Paused" && !token.IsCancellationRequested)
                    {
                        await Task.Delay(50, token);
                    }
                    if (token.IsCancellationRequested) break;

                    item.Status = "Running";
                    item.StatusColor = "#10B981";
                    item.StatusBg = "#064E3B";
                    RunningFaultDisplay = $"{item.MsgName}.{item.Signal}";

                    SignalModel? signal = Signals.FirstOrDefault(s =>
                        (s.MessageId == item.MsgId || string.Equals(s.MessageName, item.MsgName, StringComparison.OrdinalIgnoreCase)) &&
                        string.Equals(s.Name, item.Signal, StringComparison.OrdinalIgnoreCase));

                    if (signal is not null)
                    {
                        signal.IsOverridden = true;
                        if (item.FaultValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                            ulong.TryParse(item.FaultValue[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong hexVal))
                        {
                            signal.Value = hexVal;
                        }
                        else if (double.TryParse(item.FaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedVal))
                        {
                            signal.Value = parsedVal;
                        }
                        SyncSignalOverrides(signal.MessageId);
                    }

                    TimeSpan duration = FaultConfigurationViewModel.ParseTimeSpan(item.Duration, TimeSpan.FromSeconds(1));
                    if (duration > TimeSpan.Zero)
                    {
                        int elapsedMs = 0;
                        int totalMs = (int)duration.TotalMilliseconds;
                        while (elapsedMs < totalMs && !token.IsCancellationRequested)
                        {
                            if (QueueStatusText != "Paused")
                            {
                                int slice = Math.Min(50, totalMs - elapsedMs);
                                await Task.Delay(slice, token);
                                elapsedMs += slice;
                            }
                            else
                            {
                                await Task.Delay(50, token);
                            }
                        }
                    }

                    item.Status = "Completed";
                    item.StatusColor = "#94A3B8";
                    item.StatusBg = "#1E2C3A";
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on stop
            }
            catch (Exception)
            {
                // Sequence step error
            }
            finally
            {
                if (!token.IsCancellationRequested && QueueStatusText == "Running")
                {
                    await StopInjectionAsync();
                }
            }
        }

        private async Task RunDirectDurationAsync(TimeSpan duration, CancellationToken token)
        {
            try
            {
                int elapsedMs = 0;
                int totalMs = (int)duration.TotalMilliseconds;
                while (elapsedMs < totalMs && !token.IsCancellationRequested)
                {
                    if (QueueStatusText != "Paused")
                    {
                        int slice = Math.Min(50, totalMs - elapsedMs);
                        await Task.Delay(slice, token);
                        elapsedMs += slice;
                    }
                    else
                    {
                        await Task.Delay(50, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (!token.IsCancellationRequested && QueueStatusText == "Running")
                {
                    await StopInjectionAsync();
                }
            }
        }

        public void NotifyExecutionCommands()
        {
            OnPropertyChanged(nameof(CanStartInjection));
            OnPropertyChanged(nameof(CanStopInjection));
            OnPropertyChanged(nameof(CanTogglePauseInjection));
            OnPropertyChanged(nameof(CanClearQueue));
            StartInjectionCommand.NotifyCanExecuteChanged();
            StopInjectionCommand.NotifyCanExecuteChanged();
            TogglePauseInjectionCommand.NotifyCanExecuteChanged();
            ClearQueueCommand.NotifyCanExecuteChanged();
        }

        public void Dispose()
        {
            StopLiveFlushTimer();
            _executionCts?.Cancel();
            _executionCts?.Dispose();
            _executionCts = null;

            if (_ownsEngine && _engine is not null)
            {
                _ = _engine.DisposeAsync().AsTask();
            }

            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            StopLiveFlushTimer();
            _executionCts?.Cancel();
            _executionCts?.Dispose();
            _executionCts = null;

            if (_ownsEngine && _engine is not null)
            {
                await _engine.DisposeAsync();
            }

            GC.SuppressFinalize(this);
        }

        private void ProjectPlan(SimulationPlan plan)
        {
            if (plan.Document is null)
            {
                return;
            }

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
                    RawIdentifier = message.Identifier,
                    IsExtendedIdentifier = message.IsExtendedIdentifier,
                    Name = message.Name,
                    Dlc = message.PayloadLength,
                    Cycle = FormatCycle(rule),
                    GatewayMode = rule?.GatewayMode.ToString() ?? "Unconfigured",
                    SendType = rule?.SendType.ToString() ?? "Unconfigured",
                    SignalCount = message.Signals.Count,
                    IsEnabled = rule?.IsEnabled ?? false,
                    DbcSource = message
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
                        RawIdentifier = message.Identifier,
                        IsExtendedIdentifier = message.IsExtendedIdentifier,
                        IsOverridden = isOverridden,
                        Cycle = FormatCycle(rule),
                        DbcSource = signal
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
