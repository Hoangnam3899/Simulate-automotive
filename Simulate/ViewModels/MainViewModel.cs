using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

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

        public string PhysicalValueInput
        {
            get
            {
                if (HasValueDescriptions && DbcSource != null)
                {
                    var desc = DbcSource.ValueDescriptions.FirstOrDefault(d => Math.Abs(d.PhysicalValue - Value) < 1e-6);
                    if (desc != null)
                    {
                        return $"[{desc.RawValue}] {desc.Description}";
                    }
                    long raw = Factor != 0 ? (long)Math.Round((Value - Offset) / Factor) : (long)Math.Round(Value);
                    desc = DbcSource.ValueDescriptions.FirstOrDefault(d => d.RawValue == raw);
                    if (desc != null)
                    {
                        return $"[{desc.RawValue}] {desc.Description}";
                    }
                    return $"[{raw}]";
                }
                return Value.ToString("0.##", CultureInfo.InvariantCulture);
            }
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
                            Value = desc != null ? desc.PhysicalValue : (rawVal * Factor + Offset);
                            IsOverridden = true;
                        }
                    }
                }
                else
                {
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                    {
                        Value = parsed;
                        IsOverridden = true;
                    }
                    else
                    {
                        IsValueValid = false;
                        ValueColor = "#EF4444";
                        OnPropertyChanged(nameof(ValidationToolTip));
                    }
                }
                OnPropertyChanged(nameof(PhysicalValueInput));
            }
        }

        public string ValidationToolTip => IsValueValid
            ? $"Giá trị: {Value}"
            : $"⚠️ CẢNH BÁO: Giá trị {Value} vượt dải cho phép [{Min} .. {Max}]!";

        partial void OnMinChanged(double value) => ValidateRange(Value);
        partial void OnMaxChanged(double value) => ValidateRange(Value);

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
                if (desc != null)
                {
                    return $"[{desc.RawValue}] {desc.Description}";
                }
                long raw = Factor != 0 ? (long)Math.Round((physical - Offset) / Factor) : (long)Math.Round(physical);
                desc = DbcSource.ValueDescriptions.FirstOrDefault(d => d.RawValue == raw);
                if (desc != null)
                {
                    return $"[{desc.RawValue}] {desc.Description}";
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
                PhysicalValueDisplay = FormatDisplayValue(Value);
                if (Factor != 0)
                {
                    double rawCalc = (Value - Offset) / Factor;
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
                if (HasReceivedData)
                {
                    RefreshOverriddenDisplay();
                }
            }
            else
            {
                StatusText = HasReceivedData ? "● Active" : "● No Data";
                StatusColor = HasReceivedData ? "#10B981" : "#64748B";
                PhysicalValueDisplay = HasReceivedData ? FormatDisplayValue(Value) : "—";
            }
        }

        partial void OnValueChanged(double value)
        {
            ValidateRange(value);
            OnPropertyChanged(nameof(PhysicalValueInput));
            if (IsOverridden && HasReceivedData)
            {
                RefreshOverriddenDisplay();
            }
        }

        public void UpdateValue(ulong raw, double physical, DateTime timestamp)
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

        public ObservableCollection<MessageModel> Messages => Simulation.Messages;
        public ObservableCollection<SignalModel> Signals => Simulation.Signals;
        public ObservableCollection<FaultQueueModel> FaultQueue => Simulation.FaultQueue;
        public FaultConfigurationViewModel FaultConfig => Simulation.FaultConfig;

        public MainViewModel()
            : this(ApplicationComposition.CreateConnectionViewModel(), new DbcManagementViewModel(), new SimulationViewModel())
        {
        }

        public MainViewModel(ConnectionViewModel connection, SimulationViewModel simulation)
            : this(connection, new DbcManagementViewModel(), simulation)
        {
        }

        /// <summary>
        /// Initializes a view model with caller-composed connection, dbc, and simulation dependencies.
        /// </summary>
        /// <param name="connection">The connection state exposed to existing bindings.</param>
        /// <param name="dbc">The DBC management state exposed to existing bindings.</param>
        /// <param name="simulation">The simulation projection exposed to existing bindings.</param>
        public MainViewModel(ConnectionViewModel connection, DbcManagementViewModel dbc, SimulationViewModel simulation)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Dbc = dbc ?? throw new ArgumentNullException(nameof(dbc));
            Simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));

            Simulation.SetSessionProvider(() => Connection.ActiveGatewaySession);
            Connection.PropertyChanged += async (sender, args) =>
            {
                if (args.PropertyName is nameof(Connection.IsConnected) or nameof(Connection.ActiveGatewaySession))
                {
                    Simulation.NotifyExecutionCommands();

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
                Simulation.LoadDocument(document);
                if (!Simulation.IsRunning && Connection.IsConnected && Connection.ActiveGatewaySession is { IsOpen: true })
                {
                    await Simulation.StartBaselineGatewayAsync();
                }
            };

            Dbc.DocumentUnloaded += (sender, args) =>
            {
                Simulation.ClearDocument();
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
