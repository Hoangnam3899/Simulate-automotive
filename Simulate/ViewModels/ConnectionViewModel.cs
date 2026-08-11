using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.ViewModels
{
    public partial class ConnectionViewModel : ObservableObject
    {
        private readonly ICanHardwareDriver _hardwareDriver;
        private CancellationTokenSource? _operationCancellation;
        private ICanGatewaySession? _gatewaySession;
        private int _isOperationActive;

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

        [ObservableProperty]
        private bool _isCanFdEnabled = true;

        [ObservableProperty]
        private ObservableCollection<uint> _availableBaudrates = new() { 250000, 500000, 1000000 };

        [ObservableProperty]
        private uint _baudrate = 500000;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private HardwareFailure? _lastFailure;

        public ConnectionViewModel(ICanHardwareDriver hardwareDriver)
        {
            _hardwareDriver = hardwareDriver ?? throw new ArgumentNullException(nameof(hardwareDriver));
        }

        partial void OnSelectedInterfaceChanged(HardwareInterface? value)
        {
            AvailableChannels.Clear();
            if (value is not null)
            {
                foreach (HardwareChannel channel in value.Channels)
                {
                    AvailableChannels.Add(channel);
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

            NotifyCommandAvailability();
        }

        partial void OnSelectedTxChanged(HardwareChannel? value)
        {
            if (value is not null && value.DefaultBaudrate > 0)
            {
                if (!AvailableBaudrates.Contains(value.DefaultBaudrate))
                {
                    AvailableBaudrates.Add(value.DefaultBaudrate);
                }

                Baudrate = value.DefaultBaudrate;
            }

            NotifyCommandAvailability();
        }

        partial void OnSelectedRxChanged(HardwareChannel? value)
        {
            NotifyCommandAvailability();
        }

        partial void OnIsCanFdEnabledChanged(bool value)
        {
            NotifyCommandAvailability();
        }

        partial void OnBaudrateChanged(uint value)
        {
            NotifyCommandAvailability();
        }

        partial void OnIsConnectedChanged(bool value)
        {
            NotifyCommandAvailability();
        }

        partial void OnIsBusyChanged(bool value)
        {
            NotifyCommandAvailability();
        }

        [RelayCommand(CanExecute = nameof(CanRefreshInterfaces))]
        private Task RefreshInterfacesAsync()
        {
            return RunExclusiveAsync(HardwareOperation.DiscoverInterfaces, async cancellationToken =>
            {
                HardwareOperationResult<IReadOnlyList<HardwareInterface>> result =
                    await _hardwareDriver.DiscoverInterfacesAsync(cancellationToken);
                if (!result.IsSuccess)
                {
                    LastFailure = result.Failure;
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                AvailableInterfaces.Clear();
                foreach (HardwareInterface hardwareInterface in result.Value!)
                {
                    AvailableInterfaces.Add(hardwareInterface);
                }

                SelectedInterface = AvailableInterfaces.Count > 0 ? AvailableInterfaces[0] : null;
            });
        }

        private bool CanRefreshInterfaces()
        {
            return !IsBusy;
        }

        [RelayCommand(CanExecute = nameof(CanConnect))]
        private Task ConnectAsync()
        {
            return RunExclusiveAsync(HardwareOperation.OpenSession, async cancellationToken =>
            {
                if (SelectedTx is null || SelectedRx is null)
                {
                    LastFailure = new HardwareFailure(
                        HardwareOperation.OpenSession,
                        HardwareErrorCode.InvalidConfiguration,
                        "Select both TX and RX CAN channels before connecting.");
                    return;
                }

                CanGatewayOptions options;
                try
                {
                    options = IsCanFdEnabled
                        ? CanGatewayOptions.CreateFlexibleDataRate(
                            SelectedRx,
                            SelectedTx,
                            Baudrate,
                            checked(Baudrate * 4))
                        : CanGatewayOptions.CreateClassic(SelectedRx, SelectedTx, Baudrate);
                }
                catch (ArgumentException exception)
                {
                    LastFailure = CreateUnexpectedFailure(HardwareOperation.OpenSession, exception);
                    return;
                }
                catch (OverflowException exception)
                {
                    LastFailure = CreateUnexpectedFailure(HardwareOperation.OpenSession, exception);
                    return;
                }

                HardwareOperationResult<ICanGatewaySession> result =
                    await _hardwareDriver.OpenGatewaySessionAsync(options, cancellationToken);
                if (!result.IsSuccess)
                {
                    LastFailure = result.Failure;
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    await StopAndDisposeAfterCancelledOpenAsync(result.Value!);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                _gatewaySession = result.Value;
                IsConnected = true;
            });
        }

        private bool CanConnect()
        {
            return !IsBusy && !IsConnected && SelectedTx is not null && SelectedRx is not null;
        }

        [RelayCommand(CanExecute = nameof(CanDisconnect))]
        private Task DisconnectAsync()
        {
            return RunExclusiveAsync(HardwareOperation.Stop, async cancellationToken =>
            {
                ICanGatewaySession? session = _gatewaySession;
                _gatewaySession = null;
                IsConnected = false;

                if (session is null)
                {
                    return;
                }

                HardwareOperationResult stopResult = await StopSessionOffDispatcherAsync(session);
                try
                {
                    await DisposeSessionOffDispatcherAsync(session);
                }
                catch (Exception exception)
                {
                    LastFailure = CreateUnexpectedFailure(HardwareOperation.Dispose, exception);
                    return;
                }

                if (!stopResult.IsSuccess)
                {
                    LastFailure = stopResult.Failure;
                }
            });
        }

        private bool CanDisconnect()
        {
            return !IsBusy && IsConnected;
        }

        /// <summary>
        /// Requests cancellation of the currently active discovery or connection operation.
        /// </summary>
        public void CancelPendingOperation()
        {
            _operationCancellation?.Cancel();
        }

        private async Task RunExclusiveAsync(
            HardwareOperation operation,
            Func<CancellationToken, Task> operationBody)
        {
            if (Interlocked.CompareExchange(ref _isOperationActive, 1, 0) != 0)
            {
                return;
            }

            using var cancellation = new CancellationTokenSource();
            _operationCancellation = cancellation;
            IsBusy = true;
            LastFailure = null;

            try
            {
                await operationBody(cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // Cancellation is an expected user action, not a hardware failure.
            }
            catch (Exception exception)
            {
                LastFailure = CreateUnexpectedFailure(operation, exception);
            }
            finally
            {
                _operationCancellation = null;
                IsBusy = false;
                Volatile.Write(ref _isOperationActive, 0);
            }
        }

        private static HardwareFailure CreateUnexpectedFailure(
            HardwareOperation operation,
            Exception exception)
        {
            return new HardwareFailure(
                operation,
                HardwareErrorCode.Unexpected,
                $"Connection operation failed during {operation}: {exception.Message}");
        }

        private static async Task StopAndDisposeAfterCancelledOpenAsync(ICanGatewaySession session)
        {
            await StopSessionOffDispatcherAsync(session);
            await DisposeSessionOffDispatcherAsync(session);
        }

        private static Task<HardwareOperationResult> StopSessionOffDispatcherAsync(
            ICanGatewaySession session)
        {
            return Task.Run(() => session.StopAsync().AsTask());
        }

        private static Task DisposeSessionOffDispatcherAsync(ICanGatewaySession session)
        {
            return Task.Run(() => session.DisposeAsync().AsTask());
        }

        private void NotifyCommandAvailability()
        {
            RefreshInterfacesCommand.NotifyCanExecuteChanged();
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        }
    }
}
