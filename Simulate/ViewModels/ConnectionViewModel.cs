using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.ViewModels
{
    public partial class ConnectionViewModel : ObservableObject
    {
        private readonly ICanHardwareDriver _hardwareDriver;

        [ObservableProperty]
        private ObservableCollection<HardwareInterface> _availableInterfaces = new();

        [ObservableProperty]
        private HardwareInterface? _selectedInterface;

        [ObservableProperty]
        private ObservableCollection<HardwareChannel> _availableChannels = new();

        [ObservableProperty]
        private HardwareChannel? _selectedTx;

        [ObservableProperty]
        private HardwareChannel? _selectedRx;

        partial void OnSelectedInterfaceChanged(HardwareInterface? value)
        {
            AvailableChannels.Clear();
            if (value != null)
            {
                foreach (var ch in value.Channels)
                {
                    AvailableChannels.Add(ch);
                }
            }

            if (AvailableChannels.Count > 0)
            {
                SelectedTx = AvailableChannels[0];
                SelectedRx = AvailableChannels.Count > 1 ? AvailableChannels[1] : AvailableChannels[0];
            }
            else
            {
                SelectedTx = null;
                SelectedRx = null;
            }
        }

        partial void OnSelectedTxChanged(HardwareChannel? value)
        {
            if (value != null && value.DefaultBaudrate > 0)
            {
                if (!AvailableBaudrates.Contains(value.DefaultBaudrate))
                {
                    AvailableBaudrates.Add(value.DefaultBaudrate);
                }
                Baudrate = value.DefaultBaudrate;
            }
        }

        [ObservableProperty]
        private bool _isCanFdEnabled = true;

        [ObservableProperty]
        private ObservableCollection<uint> _availableBaudrates = new() { 250000, 500000, 1000000 };

        [ObservableProperty]
        private uint _baudrate = 500000;

        [ObservableProperty]
        private bool _isConnected;

        public ConnectionViewModel(ICanHardwareDriver hardwareDriver)
        {
            _hardwareDriver = hardwareDriver;
            RefreshInterfaces();
        }

        [RelayCommand]
        private void RefreshInterfaces()
        {
            var interfaces = _hardwareDriver.GetAvailableInterfaces();
            AvailableInterfaces.Clear();
            foreach (var iface in interfaces)
            {
                AvailableInterfaces.Add(iface);
            }

            if (AvailableInterfaces.Count > 0)
            {
                SelectedInterface = AvailableInterfaces[0];
            }
        }

        [RelayCommand(CanExecute = nameof(CanConnect))]
        private void Connect()
        {
            if (SelectedTx != null && SelectedRx != null)
            {
                IsConnected = _hardwareDriver.Connect(SelectedTx, SelectedRx, Baudrate, IsCanFdEnabled);
                ConnectCommand.NotifyCanExecuteChanged();
                DisconnectCommand.NotifyCanExecuteChanged();
            }
        }

        private bool CanConnect() => !IsConnected;

        [RelayCommand(CanExecute = nameof(CanDisconnect))]
        private void Disconnect()
        {
            if (_hardwareDriver.Disconnect())
            {
                IsConnected = false;
                ConnectCommand.NotifyCanExecuteChanged();
                DisconnectCommand.NotifyCanExecuteChanged();
            }
        }

        private bool CanDisconnect() => IsConnected;
    }
}
