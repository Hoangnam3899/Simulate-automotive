using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Simulate.Models;

namespace Simulate.ViewModels
{
    public partial class MessageModel : ObservableObject
    {
        [ObservableProperty]
        private int _displayIndex;

        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private uint _rawIdentifier;

        [ObservableProperty]
        private bool _isExtendedIdentifier;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private int _dlc;

        [ObservableProperty]
        private string _cycle = "—";

        [ObservableProperty]
        private string _gatewayMode = "PassThrough";

        [ObservableProperty]
        private string _sendType = "Cyclic";

        [ObservableProperty]
        private int _signalCount;

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private string _lastSent = "—";

        public Simulate.Models.DbcMessage? DbcSource { get; set; }
    }

    public partial class SignalModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private int _startBit;

        [ObservableProperty]
        private int _length;

        [ObservableProperty]
        private double _factor = 1.0;

        [ObservableProperty]
        private double _offset;

        [ObservableProperty]
        private string _unit = string.Empty;

        [ObservableProperty]
        private double _min;

        [ObservableProperty]
        private double _max;

        [ObservableProperty]
        private string _rawValue = "—";

        [ObservableProperty]
        private double _value;

        [ObservableProperty]
        private string _physicalValueDisplay = "—";

        [ObservableProperty]
        private string _messageId = string.Empty;

        [ObservableProperty]
        private uint _rawIdentifier;

        [ObservableProperty]
        private bool _isExtendedIdentifier;

        [ObservableProperty]
        private string _messageName = string.Empty;

        [ObservableProperty]
        private bool _isOverridden;

        [ObservableProperty]
        private string _cycle = "—";

        [ObservableProperty]
        private bool _hasReceivedData;

        [ObservableProperty]
        private string _statusText = "● No Data";

        [ObservableProperty]
        private string _statusColor = "#64748B";

        [ObservableProperty]
        private string _lastUpdated = "—";

        [ObservableProperty]
        private bool _isValueValid = true;

        [ObservableProperty]
        private string _valueColor = "#F8FAFC";

        private Simulate.Models.DbcSignal? _dbcSource;
        public Simulate.Models.DbcSignal? DbcSource
        {
            get => _dbcSource;
            set
            {
                _dbcSource = value;
                _availableValueDescriptions = null;
                if (!_configuredValue.HasValue)
                {
                    if (HasValueDescriptions && _dbcSource != null && _dbcSource.ValueDescriptions.Count > 0)
                    {
                        _configuredValue = _dbcSource.ValueDescriptions[0].PhysicalValue;
                    }
                    else
                    {
                        _configuredValue = Value;
                    }
                }
                OnPropertyChanged(nameof(HasValueDescriptions));
                OnPropertyChanged(nameof(AvailableValueDescriptions));
                OnPropertyChanged(nameof(PhysicalValueInput));
            }
        }

        public bool HasValueDescriptions => DbcSource?.ValueDescriptions != null && DbcSource.ValueDescriptions.Count > 0;

        private ObservableCollection<string>? _availableValueDescriptions;
        public ObservableCollection<string> AvailableValueDescriptions
        {
            get
            {
                if (_availableValueDescriptions == null)
                {
                    _availableValueDescriptions = new ObservableCollection<string>();
                    if (HasValueDescriptions && DbcSource != null)
                    {
                        foreach (var vd in DbcSource.ValueDescriptions)
                        {
                            _availableValueDescriptions.Add($"[{vd.RawValue}] {vd.Description}");
                        }
                    }
                }
                return _availableValueDescriptions;
            }
        }

        private double? _configuredValue;
        private bool _isUpdatingFromBus;

        public double ConfiguredValue
        {
            get => _configuredValue ?? Value;
            set
            {
                if (!_configuredValue.HasValue || Math.Abs(_configuredValue.Value - value) > 1e-9)
                {
                    _configuredValue = value;
                    if (IsOverridden)
                    {
                        Value = value;
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PhysicalValueInput));
                    ValidateRange(value);
                }
            }
        }

        public string PhysicalValueInput
        {
            get => FormatDisplayValue(ConfiguredValue);
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;

                if (HasValueDescriptions && DbcSource != null)
                {
                    int start = value.IndexOf('[');
                    int end = value.IndexOf(']');
                    if (start >= 0 && end > start)
                    {
                        string numStr = value.Substring(start + 1, end - start - 1);
                        if (long.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long rawVal))
                        {
                            var desc = DbcSource.ValueDescriptions.FirstOrDefault(d => d.RawValue == rawVal);
                            double chosenVal = desc != null ? desc.PhysicalValue : (rawVal * Factor + Offset);
                            ConfiguredValue = chosenVal;
                            Value = chosenVal;
                            if (HasReceivedData && IsOverridden)
                            {
                                RefreshOverriddenDisplay();
                            }
                        }
                    }
                }
                else
                {
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                    {
                        ConfiguredValue = parsed;
                        Value = parsed;
                        if (HasReceivedData && IsOverridden)
                        {
                            RefreshOverriddenDisplay();
                        }
                    }
                    else
                    {
                        IsValueValid = false;
                        ValueColor = "#EF4444";
                        OnPropertyChanged(nameof(ValidationToolTip));
                    }
                }
            }
        }

        public string ValidationToolTip => IsValueValid
            ? $"Giá trị: {Value}"
            : $"⚠️ CẢNH BÁO: Giá trị {Value} vượt dải cho phép [{Min} .. {Max}]!";

        partial void OnMinChanged(double value) => ValidateRange(ConfiguredValue);
        partial void OnMaxChanged(double value) => ValidateRange(ConfiguredValue);

        private void ValidateRange(double val)
        {
            IsValueValid = (Min == Max) || (val >= Min && val <= Max);
            ValueColor = IsValueValid ? "#F8FAFC" : "#EF4444";
            OnPropertyChanged(nameof(ValidationToolTip));
        }

        private string FormatDisplayValue(double physical)
        {
            if (HasValueDescriptions && DbcSource != null)
            {
                var desc = DbcSource.ValueDescriptions.FirstOrDefault(d => Math.Abs(d.PhysicalValue - physical) < 1e-6);
                if (desc == null && Factor != 0)
                {
                    long rawCalc = (long)Math.Round((physical - Offset) / Factor);
                    desc = DbcSource.ValueDescriptions.FirstOrDefault(d => d.RawValue == rawCalc);
                }

                if (desc != null)
                {
                    string prefix = $"[{desc.RawValue}] ";
                    string? match = AvailableValueDescriptions.FirstOrDefault(s => s.StartsWith(prefix, StringComparison.Ordinal));
                    return match ?? $"[{desc.RawValue}] {desc.Description}";
                }
            }

            return string.IsNullOrWhiteSpace(Unit)
                ? physical.ToString("0.##", CultureInfo.InvariantCulture)
                : $"{physical.ToString("0.##", CultureInfo.InvariantCulture)} {Unit}";
        }

        public void RefreshOverriddenDisplay()
        {
            if (IsOverridden)
            {
                PhysicalValueDisplay = FormatDisplayValue(ConfiguredValue);
                if (Factor != 0)
                {
                    double rawCalc = (ConfiguredValue - Offset) / Factor;
                    RawValue = rawCalc >= 0
                        ? $"0x{(ulong)Math.Round(rawCalc):X}"
                        : $"0x{(long)Math.Round(rawCalc):X}";
                }
                StatusText = "● Injected";
                StatusColor = "#EF4444";
            }
        }

        partial void OnIsOverriddenChanged(bool value)
        {
            if (value)
            {
                Value = ConfiguredValue;
                if (HasReceivedData)
                {
                    RefreshOverriddenDisplay();
                }
                OnPropertyChanged(nameof(PhysicalValueInput));
            }
            else
            {
                StatusText = HasReceivedData ? "● Active" : "● No Data";
                StatusColor = HasReceivedData ? "#10B981" : "#64748B";
                PhysicalValueDisplay = HasReceivedData ? FormatDisplayValue(Value) : "—";
                if (HasReceivedData && Factor != 0)
                {
                    double rawCalc = (Value - Offset) / Factor;
                    RawValue = rawCalc >= 0
                        ? $"0x{(ulong)Math.Round(rawCalc):X}"
                        : $"0x{(long)Math.Round(rawCalc):X}";
                }
                OnPropertyChanged(nameof(PhysicalValueInput));
            }
        }

        partial void OnValueChanged(double value)
        {
            ValidateRange(value);
            if (!_isUpdatingFromBus)
            {
                _configuredValue = value;
            }
            if (IsOverridden)
            {
                OnPropertyChanged(nameof(PhysicalValueInput));
                if (HasReceivedData)
                {
                    RefreshOverriddenDisplay();
                }
            }
        }

        public void UpdateValue(ulong raw, double physical, DateTime timestamp)
        {
            _isUpdatingFromBus = true;
            try
            {
                HasReceivedData = true;
                LastUpdated = timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

                if (IsOverridden)
                {
                    RefreshOverriddenDisplay();
                }
                else
                {
                    Value = physical;
                    RawValue = $"0x{raw:X}";
                    PhysicalValueDisplay = FormatDisplayValue(physical);
                    StatusText = "● Active";
                    StatusColor = "#10B981";
                }
            }
            finally
            {
                _isUpdatingFromBus = false;
            }
        }

        public void ResetData()
        {
            RawValue = "—";
            if (!IsOverridden)
            {
                Value = Offset;
                PhysicalValueDisplay = "—";
            }
            HasReceivedData = false;
            StatusText = IsOverridden ? "● Injected" : "● No Data";
            StatusColor = IsOverridden ? "#EF4444" : "#64748B";
            LastUpdated = "—";
        }
    }

    public class FaultQueueModel
    {
        public int Index { get; set; }
        public string MsgId { get; set; } = string.Empty;
        public string MsgName { get; set; } = string.Empty;
        public string Signal { get; set; } = string.Empty;
        public string FaultType { get; set; } = string.Empty;
        public string FaultValue { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Repeat { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "#10B981";
        public string StatusBg { get; set; } = "#064E3B";
    }

    public partial class MainViewModel : ObservableObject
    {
        private readonly object _shutdownSync = new();
        private Task? _shutdownTask;

        public ConnectionViewModel Connection { get; }

        public DbcManagementViewModel Dbc { get; }

        public SimulationViewModel Simulation { get; }

        public LoggingViewModel Logging { get; }

        public BusHealthViewModel BusHealth { get; }

        public StatusOverviewViewModel StatusOverview { get; }

        public ObservableCollection<MessageModel> Messages => Simulation.Messages;
        public ObservableCollection<SignalModel> Signals => Simulation.Signals;
        public ObservableCollection<FaultQueueModel> FaultQueue => Simulation.FaultQueue;
        public FaultConfigurationViewModel FaultConfig => Simulation.FaultConfig;

        public MainViewModel()
            : this(ApplicationComposition.CreateConnectionViewModel(), new DbcManagementViewModel(), new SimulationViewModel(), new LoggingViewModel())
        {
        }

        public MainViewModel(ConnectionViewModel connection, SimulationViewModel simulation)
            : this(connection, new DbcManagementViewModel(), simulation, new LoggingViewModel())
        {
        }

        public MainViewModel(ConnectionViewModel connection, DbcManagementViewModel dbc, SimulationViewModel simulation)
            : this(connection, dbc, simulation, new LoggingViewModel())
        {
        }

        /// <summary>
        /// Initializes a view model with caller-composed connection, dbc, simulation, and logging dependencies.
        /// </summary>
        /// <param name="connection">The connection state exposed to existing bindings.</param>
        /// <param name="dbc">The DBC management state exposed to existing bindings.</param>
        /// <param name="simulation">The simulation projection exposed to existing bindings.</param>
        /// <param name="logging">The logging projection exposed to existing bindings.</param>
        public MainViewModel(ConnectionViewModel connection, DbcManagementViewModel dbc, SimulationViewModel simulation, LoggingViewModel logging)
            : this(connection, dbc, simulation, logging, null)
        {
        }

        /// <summary>
        /// Initializes a view model with caller-composed connection, dbc, simulation, logging, and bus health dependencies.
        /// </summary>
        public MainViewModel(ConnectionViewModel connection, DbcManagementViewModel dbc, SimulationViewModel simulation, LoggingViewModel logging, BusHealthViewModel? busHealth)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Dbc = dbc ?? throw new ArgumentNullException(nameof(dbc));
            Simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            Logging = logging ?? throw new ArgumentNullException(nameof(logging));

            BusHealth = busHealth ?? new BusHealthViewModel(
                () => Connection.IsConnected,
                () => Simulation.IsRunning,
                () => (uint)(Connection.BaudrateTx * 1000),
                () => Simulation.CurrentEngine?.Statistics ?? Simulation.Statistics,
                () => Connection.LastFailure ?? Simulation.CurrentEngine?.LastFailure ?? Simulation.LastFailure,
                () => 0);

            StatusOverview = new StatusOverviewViewModel(Connection, Dbc, Simulation, BusHealth);

            Logging.LogService.LogAdded += (sender, entry) =>
            {
                if (entry.Level == LogLevel.Warning)
                {
                    BusHealth.IncrementWarningCount();
                }
                else if (entry.Level == LogLevel.Error)
                {
                    BusHealth.IncrementErrorCount();
                }
            };

            Logging.LogService.Cleared += (sender, e) =>
            {
                BusHealth.ClearWarnings();
                BusHealth.ClearErrors();
            };

            Logging.LogService.LogInfo("System", "AFI - Automotive Fault Injector initialized.");

            Simulation.EngineFaulted += failure =>
            {
                Logging.LogService.LogError("Engine", $"Gateway engine fault: {failure.Operation} ({failure.Code}) - {failure.Message}");
                BusHealth.IncrementErrorCount();
                BusHealth.UpdateTelemetry();
            };

            Simulation.SetSessionProvider(() => Connection.ActiveGatewaySession);
            Connection.PropertyChanged += async (sender, args) =>
            {
                if (args.PropertyName == nameof(Connection.IsConnected))
                {
                    if (Connection.IsConnected)
                    {
                        string iface = Connection.SelectedInterface?.Name ?? "CAN Interface";
                        string tx = Connection.SelectedTx?.Name ?? "TX";
                        string rx = Connection.SelectedRx?.Name ?? "RX";
                        string baud = $"{Connection.BaudrateTx}/{Connection.BaudrateRx} kbps";
                        string fd = Connection.IsCanFdEnabled ? "FD Enabled" : "Classic";
                        Logging.LogService.LogInfo("Connection", $"Connected to {iface} (TX: {tx}, RX: {rx}, {baud}, {fd}).");
                        BusHealth.UpdateTelemetry();
                    }
                    else
                    {
                        Logging.LogService.LogInfo("Connection", "CAN gateway session disconnected.");
                        BusHealth.Reset();
                    }
                }
                else if (args.PropertyName == nameof(Connection.LastFailure) && Connection.LastFailure is { } failure)
                {
                    Logging.LogService.LogError("Connection", $"Hardware failure: {failure.Operation} ({failure.Code}) - {failure.Message}");
                    BusHealth.IncrementErrorCount();
                }

                if (args.PropertyName is nameof(Connection.IsConnected) or nameof(Connection.ActiveGatewaySession))
                {
                    Simulation.NotifyExecutionCommands();

                    if (Connection.ActiveGatewaySession is { } activeSession)
                    {
                        activeSession.FrameLossDetected += () =>
                        {
                            Logging.LogService.LogWarning("Hardware", "Hardware CAN receive buffer overflow reported — frame(s) lost.");
                            BusHealth.UpdateTelemetry();
                        };
                    }

                    if (Connection.IsConnected && Connection.ActiveGatewaySession is { IsOpen: true })
                    {
                        await Simulation.StartBaselineGatewayAsync();
                    }
                    else if (!Connection.IsConnected)
                    {
                        await Simulation.StopGatewayAsync();
                    }
                }
            };

            Dbc.DocumentLoaded += async (sender, document) =>
            {
                string fileName = Dbc.LoadedFileName ?? "DBC";
                Logging.LogService.LogInfo("DBC", $"Loaded DBC file '{fileName}': {document.Messages.Count} messages, {document.Messages.Sum(m => m.Signals.Count)} signals.");
                Simulation.LoadDocument(document);

                foreach (SignalModel signal in Simulation.Signals)
                {
                    HookSignalEvents(signal);
                }

                if (!Simulation.IsRunning && Connection.IsConnected && Connection.ActiveGatewaySession is { IsOpen: true })
                {
                    await Simulation.StartBaselineGatewayAsync();
                }
            };

            Dbc.DocumentUnloaded += (sender, args) =>
            {
                Logging.LogService.LogInfo("DBC", "DBC document unloaded.");
                Simulation.ClearDocument();
            };

            Simulation.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName is nameof(Simulation.IsRunning) or nameof(Simulation.LastFailure))
                {
                    BusHealth.UpdateTelemetry();
                }

                if (args.PropertyName == nameof(Simulation.QueueStatusText))
                {
                    if (Simulation.QueueStatusText == "Running")
                    {
                        string mode = Simulation.FaultConfig.SelectedInjectionMode ?? "Direct";
                        Logging.LogService.LogInfo("Execution", $"Fault injection running (Mode: {mode}).");
                    }
                    else if (Simulation.QueueStatusText == "Paused")
                    {
                        Logging.LogService.LogInfo("Execution", "Fault injection paused.");
                    }
                    else if (Simulation.QueueStatusText == "Idle")
                    {
                        Logging.LogService.LogInfo("Execution", "Fault injection stopped / idle.");
                    }
                }
            };

            Simulation.FaultQueue.CollectionChanged += (sender, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Add && args.NewItems is not null)
                {
                    foreach (FaultQueueModel item in args.NewItems)
                    {
                        Logging.LogService.LogInfo("Fault", $"Added fault rule to queue: {item.Signal} | {item.FaultType}: {item.FaultValue} | Mode: {item.Mode}.");
                    }
                }
                else if (args.Action == NotifyCollectionChangedAction.Reset || (args.Action == NotifyCollectionChangedAction.Remove && Simulation.FaultQueue.Count == 0))
                {
                    Logging.LogService.LogInfo("Fault", "Fault queue cleared.");
                }
            };

            Simulation.Signals.CollectionChanged += (sender, args) =>
            {
                if (args.NewItems is not null)
                {
                    foreach (SignalModel signal in args.NewItems)
                    {
                        HookSignalEvents(signal);
                    }
                }
            };
        }

        private void HookSignalEvents(SignalModel signal)
        {
            signal.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SignalModel.IsOverridden))
                {
                    Logging.LogService.LogInfo("Fault", $"Signal '{signal.Name}' override {(signal.IsOverridden ? "enabled" : "disabled")} (Value: {signal.Value} {signal.Unit}).");
                }
                else if (signal.IsOverridden && e.PropertyName == nameof(SignalModel.Value))
                {
                    Logging.LogService.LogInfo("Fault", $"Signal '{signal.Name}' value set to {signal.Value} {signal.Unit}.");
                }
                else if (e.PropertyName == nameof(SignalModel.IsValueValid) && !signal.IsValueValid)
                {
                    Logging.LogService.LogWarning("Fault", $"Value {signal.Value} for signal '{signal.Name}' is outside defined range [{signal.Min} .. {signal.Max}].");
                }
            };
        }

        /// <summary>
        /// Stops configured simulation work before releasing the connection-owned gateway session.
        /// Repeated calls share one shutdown operation.
        /// </summary>
        public Task ShutdownAsync()
        {
            TaskCompletionSource<object?> completion;
            lock (_shutdownSync)
            {
                if (_shutdownTask is not null)
                {
                    return _shutdownTask;
                }

                completion = new TaskCompletionSource<object?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _shutdownTask = completion.Task;
            }

            _ = CompleteShutdownAsync(completion);
            return completion.Task;
        }

        private async Task ShutdownCoreAsync()
        {
            try
            {
                await Simulation.StopGatewayAsync();
            }
            finally
            {
                StatusOverview.Dispose();
                await Connection.ShutdownAsync();
            }
        }

        private async Task CompleteShutdownAsync(TaskCompletionSource<object?> completion)
        {
            try
            {
                await ShutdownCoreAsync();
                completion.TrySetResult(null);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }
    }
}
