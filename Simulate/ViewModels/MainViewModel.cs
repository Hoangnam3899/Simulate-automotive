using System;
using System.Collections.ObjectModel;
using System.Globalization;
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

        public Simulate.Models.DbcSignal? DbcSource { get; set; }

        public void UpdateValue(ulong raw, double physical, DateTime timestamp)
        {
            RawValue = $"0x{raw:X}";
            Value = physical;
            PhysicalValueDisplay = string.IsNullOrWhiteSpace(Unit)
                ? physical.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                : $"{physical.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} {Unit}";
            HasReceivedData = true;
            StatusText = IsOverridden ? "● Injected" : "● Active";
            StatusColor = IsOverridden ? "#EF4444" : "#10B981";
            LastUpdated = timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        public void ResetData()
        {
            RawValue = "—";
            Value = Offset;
            PhysicalValueDisplay = "—";
            HasReceivedData = false;
            StatusText = "● No Data";
            StatusColor = "#64748B";
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

            Dbc.DocumentLoaded += (sender, document) => Simulation.LoadDocument(document);
            Dbc.DocumentUnloaded += (sender, args) => Simulation.ClearDocument();
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
                if (Simulation.IsConfigured)
                {
                    await Simulation.StopAsync();
                }
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
