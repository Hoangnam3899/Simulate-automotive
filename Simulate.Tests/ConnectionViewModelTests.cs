using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;
using Simulate.ViewModels;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class ConnectionViewModelTests
    {
        [TestMethod]
        public void Constructor_does_not_start_hardware_discovery()
        {
            var driver = new ControllableHardwareDriver();

            var viewModel = new ConnectionViewModel(driver);

            Assert.AreEqual(0, driver.DiscoveryCallCount);
            Assert.AreEqual(0, viewModel.AvailableInterfaces.Count);
            Assert.IsFalse(viewModel.IsBusy);
        }

        [TestMethod]
        public async Task Refresh_populates_interfaces_and_selects_default_channels()
        {
            var viewModel = new ConnectionViewModel(new MockHardwareService());

            await viewModel.RefreshInterfacesCommand.ExecuteAsync(null);

            Assert.AreEqual(2, viewModel.AvailableInterfaces.Count);
            Assert.IsNotNull(viewModel.SelectedInterface);
            Assert.IsNotNull(viewModel.SelectedTx);
            Assert.IsNotNull(viewModel.SelectedRx);
            Assert.IsFalse(viewModel.IsBusy);
            Assert.IsNull(viewModel.LastFailure);
        }

        [TestMethod]
        public async Task Connect_and_disconnect_use_the_async_gateway_session_seam()
        {
            var viewModel = new ConnectionViewModel(new MockHardwareService());
            await viewModel.RefreshInterfacesCommand.ExecuteAsync(null);

            await viewModel.ConnectCommand.ExecuteAsync(null);

            Assert.IsTrue(viewModel.IsConnected);
            Assert.IsFalse(viewModel.IsBusy);
            Assert.IsNull(viewModel.LastFailure);

            await viewModel.DisconnectCommand.ExecuteAsync(null);

            Assert.IsFalse(viewModel.IsConnected);
            Assert.IsFalse(viewModel.IsBusy);
            Assert.IsNull(viewModel.LastFailure);
        }

        [TestMethod]
        public async Task Connect_surfaces_the_typed_gateway_open_failure()
        {
            var driver = new MockHardwareService(
                new MockHardwareFaultPlan(MockHardwareFaultPoint.OpenDriver));
            var viewModel = new ConnectionViewModel(driver);
            await viewModel.RefreshInterfacesCommand.ExecuteAsync(null);

            await viewModel.ConnectCommand.ExecuteAsync(null);

            Assert.IsFalse(viewModel.IsConnected);
            Assert.IsNotNull(viewModel.LastFailure);
            Assert.AreEqual(HardwareOperation.OpenDriver, viewModel.LastFailure.Operation);
            Assert.AreEqual(HardwareErrorCode.DriverUnavailable, viewModel.LastFailure.Code);
        }

        [TestMethod]
        public async Task Cancellation_ends_the_active_refresh_and_keeps_other_commands_disabled_while_busy()
        {
            var driver = new ControllableHardwareDriver();
            var viewModel = new ConnectionViewModel(driver);

            Task refreshTask = viewModel.RefreshInterfacesCommand.ExecuteAsync(null);
            await driver.DiscoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.IsTrue(viewModel.IsBusy);
            Assert.IsFalse(viewModel.RefreshInterfacesCommand.CanExecute(null));
            Assert.IsFalse(viewModel.ConnectCommand.CanExecute(null));
            Assert.IsFalse(viewModel.DisconnectCommand.CanExecute(null));

            viewModel.CancelPendingOperation();
            await refreshTask;

            Assert.IsFalse(viewModel.IsBusy);
            Assert.AreEqual(0, viewModel.AvailableInterfaces.Count);
            Assert.IsNull(viewModel.LastFailure);
        }

        [TestMethod]
        public async Task Disconnect_returns_control_while_session_cleanup_runs()
        {
            CanGatewayOptions options = CreateClassicOptions();
            var session = new BlockingCleanupSession(options);
            var driver = new SingleSessionHardwareDriver(options, session);
            var viewModel = new ConnectionViewModel(driver);
            await viewModel.RefreshInterfacesCommand.ExecuteAsync(null);
            await viewModel.ConnectCommand.ExecuteAsync(null);

            var commandReturned = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task caller = Task.Run(() =>
            {
                try
                {
                    Task disconnectTask = viewModel.DisconnectCommand.ExecuteAsync(null);
                    commandReturned.TrySetResult(disconnectTask);
                }
                catch (Exception exception)
                {
                    commandReturned.TrySetException(exception);
                }
            });

            await session.StopStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            try
            {
                Assert.IsTrue(
                    commandReturned.Task.IsCompleted,
                    "Disconnect must return control before synchronous native cleanup completes.");
            }
            finally
            {
                session.AllowCleanupToComplete();
            }

            Task disconnect = await commandReturned.Task.WaitAsync(TimeSpan.FromSeconds(1));
            await disconnect;
            await caller;

            Assert.IsFalse(viewModel.IsConnected);
            Assert.IsTrue(session.IsDisposed);
        }

        private static CanGatewayOptions CreateClassicOptions()
        {
            return CanGatewayOptions.CreateClassic(
                new HardwareChannel { Name = "RX", ChannelIndex = 1, ChannelMask = 2 },
                new HardwareChannel { Name = "TX", ChannelIndex = 0, ChannelMask = 1 },
                500000);
        }

        private sealed class ControllableHardwareDriver : ICanHardwareDriver
        {
            private readonly TaskCompletionSource<HardwareOperationResult<IReadOnlyList<HardwareInterface>>>
                _discoveryCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public int DiscoveryCallCount { get; private set; }

            public TaskCompletionSource<object?> DiscoveryStarted { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<HardwareOperationResult<IReadOnlyList<HardwareInterface>>> DiscoverInterfacesAsync(
                CancellationToken cancellationToken = default)
            {
                DiscoveryCallCount++;
                DiscoveryStarted.TrySetResult(null);
                return _discoveryCompletion.Task.WaitAsync(cancellationToken);
            }

            public Task<HardwareOperationResult<ICanGatewaySession>> OpenGatewaySessionAsync(
                CanGatewayOptions options,
                CancellationToken cancellationToken = default)
            {
                throw new AssertFailedException("This test driver does not open gateway sessions.");
            }
        }

        private sealed class SingleSessionHardwareDriver : ICanHardwareDriver
        {
            private readonly CanGatewayOptions _options;
            private readonly ICanGatewaySession _session;

            public SingleSessionHardwareDriver(CanGatewayOptions options, ICanGatewaySession session)
            {
                _options = options;
                _session = session;
            }

            public Task<HardwareOperationResult<IReadOnlyList<HardwareInterface>>> DiscoverInterfacesAsync(
                CancellationToken cancellationToken = default)
            {
                IReadOnlyList<HardwareInterface> interfaces =
                [
                    new HardwareInterface
                    {
                        Name = "Test CAN",
                        Channels = new List<HardwareChannel>
                        {
                            _options.TxChannel,
                            _options.RxChannel
                        }
                    }
                ];
                return Task.FromResult(HardwareOperationResult.Succeeded(interfaces));
            }

            public Task<HardwareOperationResult<ICanGatewaySession>> OpenGatewaySessionAsync(
                CanGatewayOptions options,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HardwareOperationResult.Succeeded(_session));
            }
        }

        private sealed class BlockingCleanupSession : ICanGatewaySession
        {
            private readonly ManualResetEventSlim _cleanupMayComplete = new(initialState: false);

            public BlockingCleanupSession(CanGatewayOptions options)
            {
                Options = options;
            }

            public CanGatewayOptions Options { get; }

            public TaskCompletionSource<object?> StopStarted { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public bool IsDisposed { get; private set; }

            public bool IsOpen => !IsDisposed;

            public IAsyncEnumerable<RoutedCanFrame> ReceiveAsync(
                CancellationToken cancellationToken = default)
            {
                return EmptyFramesAsync();
            }

            public ValueTask<HardwareOperationResult> TransmitAsync(
                CanGatewaySide destination,
                CanFrame frame,
                CancellationToken cancellationToken = default)
            {
                return ValueTask.FromResult(HardwareOperationResult.Succeeded());
            }

            public ValueTask<HardwareOperationResult> FlushAsync(
                CancellationToken cancellationToken = default)
            {
                return ValueTask.FromResult(HardwareOperationResult.Succeeded());
            }

            public ValueTask<HardwareOperationResult> StopAsync()
            {
                StopStarted.TrySetResult(null);
                _cleanupMayComplete.Wait();
                return ValueTask.FromResult(HardwareOperationResult.Succeeded());
            }

            public ValueTask DisposeAsync()
            {
                IsDisposed = true;
                return ValueTask.CompletedTask;
            }

            public void AllowCleanupToComplete()
            {
                _cleanupMayComplete.Set();
            }

            private static async IAsyncEnumerable<RoutedCanFrame> EmptyFramesAsync()
            {
                await Task.CompletedTask;
                yield break;
            }
        }
    }
}
