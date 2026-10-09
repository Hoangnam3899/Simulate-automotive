using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;
using Simulate.ViewModels;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class HardwareBufferOverflowTests
    {
        private sealed class MockGatewaySession : ICanGatewaySession
        {
            public CanGatewayOptions Options { get; } = CanGatewayOptions.CreateClassic(
                new HardwareChannel { Name = "RX", ChannelIndex = 0, ChannelMask = 1 },
                new HardwareChannel { Name = "TX", ChannelIndex = 1, ChannelMask = 2 },
                500000);

            public bool IsOpen { get; set; } = true;

            public event Action? FrameLossDetected;

            public void TriggerFrameLoss() => FrameLossDetected?.Invoke();

            public async IAsyncEnumerable<RoutedCanFrame> ReceiveAsync(
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await Task.Yield();
                yield break;
            }

            public ValueTask<HardwareOperationResult> TransmitAsync(
                CanGatewaySide destination,
                CanFrame frame,
                CancellationToken cancellationToken = default)
            {
                return ValueTask.FromResult(HardwareOperationResult.Succeeded());
            }

            public ValueTask<HardwareOperationResult> FlushAsync(CancellationToken cancellationToken = default)
            {
                return ValueTask.FromResult(HardwareOperationResult.Succeeded());
            }

            public ValueTask<HardwareOperationResult> StopAsync()
            {
                IsOpen = false;
                return ValueTask.FromResult(HardwareOperationResult.Succeeded());
            }

            public ValueTask DisposeAsync()
            {
                IsOpen = false;
                return ValueTask.CompletedTask;
            }
        }

        private sealed class FakeHardwareDriver : ICanHardwareDriver
        {
            public MockGatewaySession LastCreatedSession { get; } = new();

            public Task<HardwareOperationResult<IReadOnlyList<HardwareInterface>>> DiscoverInterfacesAsync(
                CancellationToken cancellationToken = default)
            {
                var ifaces = new List<HardwareInterface>
                {
                    new()
                    {
                        Name = "VN1640",
                        Channels = new List<HardwareChannel>
                        {
                            new() { Name = "CH1", ChannelIndex = 0, ChannelMask = 1 },
                            new() { Name = "CH2", ChannelIndex = 1, ChannelMask = 2 }
                        }
                    }
                };
                return Task.FromResult(HardwareOperationResult.Succeeded<IReadOnlyList<HardwareInterface>>(ifaces));
            }

            public Task<HardwareOperationResult<ICanGatewaySession>> OpenGatewaySessionAsync(
                CanGatewayOptions options,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HardwareOperationResult.Succeeded<ICanGatewaySession>(LastCreatedSession));
            }
        }

        [TestMethod]
        public async Task FrameLoss_is_not_double_subscribed_when_connection_properties_change()
        {
            var driver = new FakeHardwareDriver();
            var connection = new ConnectionViewModel(driver);
            var logging = new LoggingViewModel();
            var mainVm = new MainViewModel(connection, new DbcManagementViewModel(), new SimulationViewModel(), logging);

            // Connect using the fake driver
            await connection.RefreshInterfacesCommand.ExecuteAsync(null);
            await connection.ConnectCommand.ExecuteAsync(null);

            Assert.IsTrue(connection.IsConnected);

            // Trigger FrameLoss once
            driver.LastCreatedSession.TriggerFrameLoss();

            // Count log warnings with source "Hardware"
            int frameLossLogs = logging.LogService.GetEntries()
                .Count(e => e.SourceModule == "Hardware" && e.Message.Contains("buffer overflow"));

            // Must be exactly 1 log, NOT duplicated (2 logs)
            Assert.AreEqual(1, frameLossLogs, "FrameLoss warning must not be printed twice due to double subscription.");
        }

        [TestMethod]
        public async Task FrameLoss_warnings_are_throttled_to_once_per_second_under_burst_losses()
        {
            var driver = new FakeHardwareDriver();
            var connection = new ConnectionViewModel(driver);
            var logging = new LoggingViewModel();
            var mainVm = new MainViewModel(connection, new DbcManagementViewModel(), new SimulationViewModel(), logging);

            await connection.RefreshInterfacesCommand.ExecuteAsync(null);
            await connection.ConnectCommand.ExecuteAsync(null);

            // Fire 5 frame loss events in rapid succession (< 1s)
            for (int i = 0; i < 5; i++)
            {
                driver.LastCreatedSession.TriggerFrameLoss();
            }

            int frameLossLogs = logging.LogService.GetEntries()
                .Count(e => e.SourceModule == "Hardware" && e.Message.Contains("buffer overflow"));

            // Throttle should suppress subsequent occurrences in the first second
            Assert.AreEqual(1, frameLossLogs, "Rapid bursts of frame losses must be throttled to 1 warning in the first second.");
        }
    }
}
