using System;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Simulate.Models;

namespace Simulate.ViewModels
{
    /// <summary>
    /// ViewModel quản lý dữ liệu tổng hợp cho Bảng 10 (STATUS OVERVIEW).
    /// Tổng hợp và đồng bộ trạng thái thời gian thực từ Connection, DBC, Simulation và BusHealth.
    /// </summary>
    public partial class StatusOverviewViewModel : ObservableObject, IDisposable
    {
        private readonly ConnectionViewModel _connection;
        private readonly DbcManagementViewModel _dbc;
        private readonly SimulationViewModel _simulation;
        private readonly BusHealthViewModel _busHealth;
        private bool _disposed;

        [ObservableProperty]
        private string _connectionText = "○ Disconnected";

        [ObservableProperty]
        private string _connectionColor = "#64748B";

        [ObservableProperty]
        private string _driverText = "None Selected";

        [ObservableProperty]
        private string _busStateText = "Offline";

        [ObservableProperty]
        private string _busStateColor = "#64748B";

        [ObservableProperty]
        private string _dbcText = "— None Loaded —";

        [ObservableProperty]
        private string _busHealthText = "Offline";

        [ObservableProperty]
        private string _busHealthColor = "#64748B";

        [ObservableProperty]
        private string _canFdText = "Disabled (Classic)";

        public StatusOverviewViewModel(
            ConnectionViewModel connection,
            DbcManagementViewModel dbc,
            SimulationViewModel simulation,
            BusHealthViewModel busHealth)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _dbc = dbc ?? throw new ArgumentNullException(nameof(dbc));
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _busHealth = busHealth ?? throw new ArgumentNullException(nameof(busHealth));

            _connection.PropertyChanged += OnDependencyPropertyChanged;
            _dbc.PropertyChanged += OnDependencyPropertyChanged;
            _dbc.DocumentLoaded += OnDocumentLoaded;
            _dbc.DocumentUnloaded += OnDocumentUnloaded;
            _simulation.PropertyChanged += OnDependencyPropertyChanged;
            _busHealth.PropertyChanged += OnDependencyPropertyChanged;

            UpdateOverviewCore();
        }

        private void OnDependencyPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdateOverview();
        }

        private void OnDocumentLoaded(object? sender, DbcDocument e)
        {
            UpdateOverview();
        }

        private void OnDocumentUnloaded(object? sender, EventArgs e)
        {
            UpdateOverview();
        }

        public void UpdateOverview()
        {
            if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(UpdateOverviewCore);
            }
            else
            {
                UpdateOverviewCore();
            }
        }

        private void UpdateOverviewCore()
        {
            if (_disposed)
            {
                return;
            }

            // 1. Connection Status
            if (_connection.IsConnected)
            {
                ConnectionText = "● Connected";
                ConnectionColor = "#10B981";
            }
            else
            {
                ConnectionText = "○ Disconnected";
                ConnectionColor = "#64748B";
            }

            // 2. Driver / Interface
            DriverText = !string.IsNullOrWhiteSpace(_connection.SelectedInterface?.Name)
                ? _connection.SelectedInterface.Name
                : "None Selected";

            // 3. Bus State
            if (!_connection.IsConnected)
            {
                BusStateText = "Offline";
                BusStateColor = "#64748B";
            }
            else if (_simulation.IsRunning || _simulation.QueueStatusText == "Running")
            {
                BusStateText = "Active";
                BusStateColor = "#10B981";
            }
            else
            {
                BusStateText = "Standby";
                BusStateColor = "#F59E0B";
            }

            // 4. DBC File & Message Count
            if (!string.IsNullOrWhiteSpace(_dbc.LoadedFileName))
            {
                int count = _dbc.MessageCount;
                DbcText = $"{_dbc.LoadedFileName} ({count} msgs)";
            }
            else
            {
                DbcText = "— None Loaded —";
            }

            // 5. Bus Health
            if (!_connection.IsConnected)
            {
                BusHealthText = "Offline";
                BusHealthColor = "#64748B";
            }
            else
            {
                string status = _busHealth.HealthStatusText;
                if (status.StartsWith("● ", StringComparison.Ordinal))
                {
                    status = "♡ " + status[2..];
                }
                BusHealthText = status;
                BusHealthColor = _busHealth.HealthStrokeColor;
            }

            // 6. CAN FD Mode & Baudrate
            if (_connection.IsCanFdEnabled)
            {
                uint dataRate = _connection.DataBaudrateTx;
                if (dataRate >= 1000000 && dataRate % 1000000 == 0)
                {
                    CanFdText = $"Enabled ({dataRate / 1000000} Mbps)";
                }
                else if (dataRate >= 1000)
                {
                    CanFdText = $"Enabled ({dataRate / 1000} kbps)";
                }
                else
                {
                    CanFdText = "Enabled (FD)";
                }
            }
            else
            {
                CanFdText = "Disabled (Classic)";
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _connection.PropertyChanged -= OnDependencyPropertyChanged;
                _dbc.PropertyChanged -= OnDependencyPropertyChanged;
                _dbc.DocumentLoaded -= OnDocumentLoaded;
                _dbc.DocumentUnloaded -= OnDocumentUnloaded;
                _simulation.PropertyChanged -= OnDependencyPropertyChanged;
                _busHealth.PropertyChanged -= OnDependencyPropertyChanged;
            }
            GC.SuppressFinalize(this);
        }
    }
}
