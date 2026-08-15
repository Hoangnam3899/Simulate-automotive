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
        private readonly object _lifecycleSync = new();
        private CancellationTokenSource? _operationCancellation;
        private TaskCompletionSource<object?>? _operationCompletion;
        private ICanGatewaySession? _gatewaySession;
        private Task? _shutdownTask;
        private bool _isOperationActive;
        private bool _isShuttingDown;

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
        private ObservableCollection<uint> _availableBaudrates = new() { 125000, 250000, 500000, 1000000 };

        [ObservableProperty]
        private uint _baudrateTx = 500000;

        [ObservableProperty]
        private uint _baudrateRx = 500000;

        [ObservableProperty]
        private ObservableCollection<uint> _availableDataBaudrates = new() { 500000, 1000000, 2000000, 4000000, 5000000, 8000000 };

        [ObservableProperty]
        private uint _dataBaudrateTx = 2000000;

        [ObservableProperty]
        private uint _dataBaudrateRx = 2000000;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private HardwareFailure? _lastFailure;

        public uint Baudrate
        {
            get => BaudrateTx;
            set
            {
                BaudrateTx = value;
                BaudrateRx = value;
            }
        }

        public uint DataBaudrate
        {
            get => DataBaudrateTx;
            set
            {
                DataBaudrateTx = value;
                DataBaudrateRx = value;
            }
        }

        public ConnectionViewModel(ICanHardwareDriver hardwareDriver)
        {
            _hardwareDriver = hardwareDriver ?? throw new ArgumentNullException(nameof(hardwareDriver));
        }

        /// <summary>
        /// Gets the currently open gateway session as a borrowed reference for application composition.
        /// The connection view model remains the sole owner responsible for stopping and disposing it.
        /// </summary>
        public ICanGatewaySession? ActiveGatewaySession
        {
            get
            {
                lock (_lifecycleSync)
                {
                    return _gatewaySession;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the disconnected connection settings can be edited safely.
        /// </summary>
        public bool CanEditConnectionSettings => !IsBusy && !IsConnected && !IsShuttingDown;

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
            }

            NotifyCommandAvailability();
        }

        partial void OnSelectedRxChanged(HardwareChannel? value)
        {
            if (value is not null && value.DefaultBaudrate > 0)
            {
                if (!AvailableBaudrates.Contains(value.DefaultBaudrate))
                {
                    AvailableBaudrates.Add(value.DefaultBaudrate);
                }
            }

            NotifyCommandAvailability();
        }

        partial void OnIsCanFdEnabledChanged(bool value)
        {
            NotifyCommandAvailability();
        }

        partial void OnBaudrateTxChanged(uint value)
        {
            NotifyCommandAvailability();
        }

        partial void OnBaudrateRxChanged(uint value)
        {
            NotifyCommandAvailability();
        }

        partial void OnDataBaudrateTxChanged(uint value)
        {
            NotifyCommandAvailability();
        }

        partial void OnDataBaudrateRxChanged(uint value)
        {
            NotifyCommandAvailability();
        }

        partial void OnIsConnectedChanged(bool value)
        {
            OnPropertyChanged(nameof(CanEditConnectionSettings));
            NotifyCommandAvailability();
        }

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(CanEditConnectionSettings));
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
            return !IsBusy && !IsConnected && !IsShuttingDown;
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
                            BaudrateRx,
                            BaudrateTx,
                            DataBaudrateRx,
                            DataBaudrateTx)
                        : CanGatewayOptions.CreateClassic(
                            SelectedRx,
                            SelectedTx,
                            BaudrateRx,
                            BaudrateTx);
                }
                catch (ArgumentException exception)
                {
                    LastFailure = CreateInvalidConfigurationFailure(exception);
                    return;
                }
                catch (OverflowException exception)
                {
                    LastFailure = CreateInvalidConfigurationFailure(exception);
                    return;
                }

                HardwareOperationResult<ICanGatewaySession> result =
                    await _hardwareDriver.OpenGatewaySessionAsync(options, cancellationToken);
                if (!result.IsSuccess)
                {
                    LastFailure = result.Failure;
                    return;
                }

                bool sessionPublished;
                lock (_lifecycleSync)
                {
                    sessionPublished = !_isShuttingDown && !cancellationToken.IsCancellationRequested;
                    if (sessionPublished)
                    {
                        _gatewaySession = result.Value;
                    }
                }

                if (!sessionPublished)
                {
                    HardwareFailure? cleanupFailure = await StopAndDisposeSessionAsync(result.Value!);
                    if (cleanupFailure is not null)
                    {
                        LastFailure = cleanupFailure;
                    }

                    return;
                }

                OnPropertyChanged(nameof(ActiveGatewaySession));
                IsConnected = true;
            });
        }

        private bool CanConnect()
        {
            return !IsBusy && !IsConnected && !IsShuttingDown &&
                SelectedTx is not null && SelectedRx is not null;
        }

        [RelayCommand(CanExecute = nameof(CanDisconnect))]
        private Task DisconnectAsync()
        {
            return RunExclusiveAsync(HardwareOperation.Stop, _ => DisconnectActiveSessionAsync());
        }

        private bool CanDisconnect()
        {
            return !IsBusy && IsConnected && !IsShuttingDown;
        }

        /// <summary>
        /// Requests cancellation of the currently active discovery or connection operation.
        /// </summary>
        public void CancelPendingOperation()
        {
            CancellationTokenSource? cancellation;
            lock (_lifecycleSync)
            {
                cancellation = _operationCancellation;
            }

            try
            {
                cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The active operation completed between capture and cancellation.
            }
        }

        /// <summary>
        /// Cancels any active discovery or connection operation, then stops and disposes the owned session once.
        /// Repeated calls return the same shutdown task and cannot reopen the connection.
        /// </summary>
        public Task ShutdownAsync()
        {
            TaskCompletionSource<object?> completion;
            lock (_lifecycleSync)
            {
                if (_shutdownTask is not null)
                {
                    return _shutdownTask;
                }

                _isShuttingDown = true;
                completion = new TaskCompletionSource<object?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _shutdownTask = completion.Task;
            }

            _ = CompleteShutdownAsync(completion);
            return completion.Task;
        }

        private async Task RunExclusiveAsync(
            HardwareOperation operation,
            Func<CancellationToken, Task> operationBody)
        {
            using var cancellation = new CancellationTokenSource();
            var completion = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_lifecycleSync)
            {
                if (_isOperationActive || _isShuttingDown)
                {
                    return;
                }

                _isOperationActive = true;
                _operationCancellation = cancellation;
                _operationCompletion = completion;
            }

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
                IsBusy = false;
                lock (_lifecycleSync)
                {
                    _operationCancellation = null;
                    _operationCompletion = null;
                    _isOperationActive = false;
                }

                completion.TrySetResult(null);
            }
        }

        private async Task ShutdownCoreAsync()
        {
            OnPropertyChanged(nameof(CanEditConnectionSettings));
            NotifyCommandAvailability();

            Task activeOperation;
            CancellationTokenSource? cancellation;
            lock (_lifecycleSync)
            {
                activeOperation = _operationCompletion?.Task ?? Task.CompletedTask;
                cancellation = _operationCancellation;
            }

            try
            {
                cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The operation completed after its task was captured.
            }

            await activeOperation;
            await DisconnectActiveSessionAsync();
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

        private async Task DisconnectActiveSessionAsync()
        {
            ICanGatewaySession? session;
            lock (_lifecycleSync)
            {
                session = _gatewaySession;
                _gatewaySession = null;
            }

            if (session is not null)
            {
                OnPropertyChanged(nameof(ActiveGatewaySession));
            }

            IsConnected = false;
            if (session is null)
            {
                return;
            }

            HardwareFailure? cleanupFailure = await StopAndDisposeSessionAsync(session);
            if (cleanupFailure is not null)
            {
                LastFailure = cleanupFailure;
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

        private static HardwareFailure CreateInvalidConfigurationFailure(Exception exception)
        {
            return new HardwareFailure(
                HardwareOperation.OpenSession,
                HardwareErrorCode.InvalidConfiguration,
                $"CAN gateway configuration is invalid: {exception.Message}");
        }

        private static async Task<HardwareFailure?> StopAndDisposeSessionAsync(
            ICanGatewaySession session)
        {
            HardwareFailure? firstFailure = null;

            try
            {
                HardwareOperationResult stopResult = await StopSessionOffDispatcherAsync(session);
                if (!stopResult.IsSuccess)
                {
                    firstFailure = stopResult.Failure;
                }
            }
            catch (Exception exception)
            {
                firstFailure = CreateUnexpectedFailure(HardwareOperation.Stop, exception);
            }

            try
            {
                await DisposeSessionOffDispatcherAsync(session);
            }
            catch (Exception exception)
            {
                firstFailure ??= CreateUnexpectedFailure(HardwareOperation.Dispose, exception);
            }

            return firstFailure;
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

        private bool IsShuttingDown
        {
            get
            {
                lock (_lifecycleSync)
                {
                    return _isShuttingDown;
                }
            }
        }
    }
}
