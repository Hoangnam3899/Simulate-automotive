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
        private ObservableCollection<HardwareChannel> _availableInterfaces = new();

        [ObservableProperty]
        private HardwareChannel? _selectedTx;

        [ObservableProperty]
        private HardwareChannel? _selectedRx;

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
            var channels = _hardwareDriver.GetAvailableChannels();
            AvailableInterfaces.Clear();
            foreach (var ch in channels)
            {
                AvailableInterfaces.Add(ch);
            }

            if (AvailableInterfaces.Count > 0)
            {
                SelectedTx = AvailableInterfaces[0];
                SelectedRx = AvailableInterfaces.Count > 1 ? AvailableInterfaces[1] : AvailableInterfaces[0];
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
