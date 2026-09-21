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
    public sealed class ExecutionControlTests
    {
        [TestMethod]
        public void FaultConfigurationViewModel_Helpers_ParseTimesAndValues()
        {
            var queue = new System.Collections.ObjectModel.ObservableCollection<FaultQueueModel>();
            var faultVm = new FaultConfigurationViewModel(queue)
            {
                CycleTimeText = "250 ms",
                StartDelayText = "50 ms",
                DurationText = "5 s",
                RepeatCountText = "10",
                FaultValueText = "0x1A"
            };

            Assert.AreEqual(TimeSpan.FromMilliseconds(250), faultVm.GetCycleInterval());
            Assert.AreEqual(TimeSpan.FromMilliseconds(50), faultVm.GetStartDelay());
            Assert.AreEqual(TimeSpan.FromSeconds(5), faultVm.GetDuration());
            Assert.AreEqual(10, faultVm.GetRepeatCount());
            Assert.AreEqual(26.0, faultVm.GetFaultValue());

            faultVm.FaultValueText = "123.45";
            Assert.AreEqual(123.45, faultVm.GetFaultValue(), 1e-4);
        }

        [TestMethod]
        public async Task StartInjection_WithPreconfiguredEngine_StartsAndTransitionsToRunning()
        {
            DbcDocument doc = ParseTestDocument();
            var engine = new FakeEngine();
            using var vm = new SimulationViewModel(new SimulationPlan(doc, []), engine);

            Assert.AreEqual("Idle", vm.QueueStatusText);
            Assert.AreEqual("0", vm.QueueItemsDisplay);
            Assert.AreEqual("—", vm.RunningFaultDisplay);
            Assert.IsTrue(vm.CanStartInjection);
            Assert.IsFalse(vm.CanStopInjection);
            Assert.IsFalse(vm.CanTogglePauseInjection);

            await vm.StartInjectionCommand.ExecuteAsync(null);

            Assert.AreEqual("Running", vm.QueueStatusText);
            Assert.AreEqual("Ⅱ  Pause", vm.PauseInjectionButtonContent);
            Assert.IsTrue(engine.IsRunning);
            Assert.IsTrue(engine.IsScheduling);
            Assert.IsFalse(vm.CanStartInjection);
            Assert.IsTrue(vm.CanStopInjection);
            Assert.IsTrue(vm.CanTogglePauseInjection);

            await vm.StopInjectionCommand.ExecuteAsync(null);
            Assert.AreEqual("Idle", vm.QueueStatusText);
            Assert.IsFalse(engine.IsScheduling);
            Assert.IsTrue(engine.IsRunning);

            await vm.StopGatewayAsync();
            Assert.IsFalse(engine.IsRunning);
        }

        [TestMethod]
        public async Task TogglePauseInjection_TogglesBetweenPausedAndRunning()
        {
            DbcDocument doc = ParseTestDocument();
            var engine = new FakeEngine();
            using var vm = new SimulationViewModel(new SimulationPlan(doc, []), engine);

            await vm.StartInjectionCommand.ExecuteAsync(null);
            Assert.AreEqual("Running", vm.QueueStatusText);

            vm.TogglePauseInjectionCommand.Execute(null);
            Assert.AreEqual("Paused", vm.QueueStatusText);
            Assert.AreEqual("▶  Resume", vm.PauseInjectionButtonContent);
            Assert.IsTrue(engine.IsSchedulingPaused);

            vm.TogglePauseInjectionCommand.Execute(null);
            Assert.AreEqual("Running", vm.QueueStatusText);
            Assert.AreEqual("Ⅱ  Pause", vm.PauseInjectionButtonContent);
            Assert.IsFalse(engine.IsSchedulingPaused);

            await vm.StopInjectionCommand.ExecuteAsync(null);
            Assert.AreEqual("Idle", vm.QueueStatusText);
        }

        [TestMethod]
        public async Task StopInjection_StopsEngineAndRestoresOverridesWhenConfigured()
        {
            DbcDocument doc = ParseTestDocument();
            var engine = new FakeEngine();
            using var vm = new SimulationViewModel(new SimulationPlan(doc, []), engine);

            SignalModel signal = vm.Signals[0];
            signal.IsOverridden = true;
            signal.Value = 99.0;
            vm.FaultConfig.IsRestoreAfterStop = true;

            await vm.StartInjectionCommand.ExecuteAsync(null);
            Assert.IsTrue(signal.IsOverridden);

            await vm.StopInjectionCommand.ExecuteAsync(null);
            Assert.AreEqual("Idle", vm.QueueStatusText);
            Assert.AreEqual("—", vm.RunningFaultDisplay);
            Assert.IsFalse(signal.IsOverridden);
        }

        [TestMethod]
        public async Task StopInjection_DoesNotRestoreOverridesWhenRestoreAfterStopIsFalse()
        {
            DbcDocument doc = ParseTestDocument();
            var engine = new FakeEngine();
            using var vm = new SimulationViewModel(new SimulationPlan(doc, []), engine);

            SignalModel signal = vm.Signals[0];
            signal.IsOverridden = true;
            signal.Value = 99.0;
            vm.FaultConfig.IsRestoreAfterStop = false;

            await vm.StartInjectionCommand.ExecuteAsync(null);
            await vm.StopInjectionCommand.ExecuteAsync(null);

            Assert.AreEqual("Idle", vm.QueueStatusText);
            Assert.IsTrue(signal.IsOverridden);
        }

        [TestMethod]
        public void ClearQueue_ClearsAllItemsAndUpdatesDisplay()
        {
            DbcDocument doc = ParseTestDocument();
            using var vm = new SimulationViewModel(new SimulationPlan(doc, []), new FakeEngine());

            vm.FaultQueue.Add(new FaultQueueModel { Index = 1, Signal = "Speed", Status = "Queued" });
            vm.FaultQueue.Add(new FaultQueueModel { Index = 2, Signal = "RPM", Status = "Queued" });

            Assert.AreEqual("2", vm.QueueItemsDisplay);
            Assert.IsTrue(vm.CanClearQueue);

            vm.ClearQueueCommand.Execute(null);

            Assert.AreEqual("0", vm.QueueItemsDisplay);
            Assert.AreEqual(0, vm.FaultQueue.Count);
            Assert.IsFalse(vm.CanClearQueue);
        }

        [TestMethod]
        public async Task StartInjection_WithSessionProviderAndDbc_BuildsPlanAndStartsEngine()
        {
            var driver = new MockHardwareService();
            var discovery = await driver.DiscoverInterfacesAsync();
            var iface = discovery.Value![0];
            var options = CanGatewayOptions.CreateClassic(iface.Channels[0], iface.Channels[1], 500000);
            var sessionResult = await driver.OpenGatewaySessionAsync(options);
            await using ICanGatewaySession session = sessionResult.Value!;

            using var vm = new SimulationViewModel();
            vm.SetSessionProvider(() => session);

            DbcDocument doc = ParseTestDocument();
            vm.LoadDocument(doc);
            vm.AddMessage(doc.Messages[0]);

            Assert.IsTrue(vm.CanStartInjection);

            await vm.StartInjectionCommand.ExecuteAsync(null);

            Assert.IsNotNull(vm.CurrentEngine);
            Assert.AreEqual("Running", vm.QueueStatusText);

            await vm.StopInjectionCommand.ExecuteAsync(null);
            Assert.AreEqual("Idle", vm.QueueStatusText);
        }

        [TestMethod]
        public async Task MainViewModel_WiresSessionProviderAndConnectionChange()
        {
            var connection = new ConnectionViewModel(new MockHardwareService());
            await connection.RefreshInterfacesCommand.ExecuteAsync(null);
            await connection.ConnectCommand.ExecuteAsync(null);

            DbcDocument doc = ParseTestDocument();
            using var simulation = new SimulationViewModel(new SimulationPlan(doc, []), new FakeEngine());
            var main = new MainViewModel(connection, simulation);

            Assert.IsNotNull(simulation.ActiveSession);
            Assert.IsTrue(simulation.ActiveSession.IsOpen);

            await connection.DisconnectCommand.ExecuteAsync(null);
            Assert.IsNull(simulation.ActiveSession);
        }

        [TestMethod]
        public async Task SequenceQueue_ExecutesStepsSequentially()
        {
            DbcDocument doc = ParseTestDocument();
            var engine = new FakeEngine();
            using var vm = new SimulationViewModel(new SimulationPlan(doc, []), engine);

            vm.FaultConfig.SelectedInjectionMode = "Sequence";
            vm.FaultQueue.Add(new FaultQueueModel
            {
                Index = 1,
                MsgId = "0x123",
                MsgName = "VehicleData",
                Signal = "VehicleSpeed",
                FaultValue = "75.0",
                Duration = "30 ms",
                Status = "Queued"
            });

            await vm.StartInjectionCommand.ExecuteAsync(null);
            Assert.AreEqual("Running", vm.QueueStatusText);

            // Wait for duration to pass and complete
            for (int i = 0; i < 20; i++)
            {
                if (vm.FaultQueue[0].Status == "Completed") break;
                await Task.Delay(20);
            }

            Assert.AreEqual("Completed", vm.FaultQueue[0].Status);
            await vm.StopInjectionCommand.ExecuteAsync(null);
        }

        [TestMethod]
        public void Gateway_ProcessIncomingFrame_UpdatesLiveSignalMonitorDirectly()
        {
            DbcDocument doc = ParseTestDocument();
            var engine = new FakeEngine();
            using var vm = new SimulationViewModel(new SimulationPlan(doc, []), engine);

            SignalModel speedSig = vm.Signals[0];
            Assert.AreEqual("● No Data", speedSig.StatusText);
            Assert.IsFalse(speedSig.HasReceivedData);

            // VehicleSpeed is at startbit 0, length 8, factor 0.5. Payload byte 0 = 100 -> 50.0 km/h
            byte[] payload = new byte[] { 100, 0 };
            vm.ProcessIncomingFrame(291, false, payload);

            Assert.IsTrue(speedSig.HasReceivedData);
            Assert.AreEqual("● Active", speedSig.StatusText);
            Assert.AreEqual(50.0, speedSig.Value, 1e-3);
            Assert.AreEqual("50 km/h", speedSig.PhysicalValueDisplay);
        }

        [TestMethod]
        public async Task StartInjection_WhenGatewayAlreadyRunning_UpdatesPlanWithoutStoppingBridge()
        {
            DbcDocument doc = ParseTestDocument();
            var engine = new FakeEngine();
            using var vm = new SimulationViewModel(new SimulationPlan(doc, []), engine);

            await vm.StartAsync();
            Assert.IsTrue(engine.IsRunning);
            Assert.IsFalse(engine.IsScheduling);

            await vm.StartInjectionCommand.ExecuteAsync(null);
            Assert.IsTrue(engine.IsRunning);
            Assert.IsTrue(engine.IsScheduling);
            Assert.AreEqual("Running", vm.QueueStatusText);

            await vm.StopInjectionCommand.ExecuteAsync(null);
            Assert.AreEqual("Idle", vm.QueueStatusText);
            Assert.IsFalse(engine.IsScheduling);
            Assert.IsTrue(engine.IsRunning);
        }

        [TestMethod]
        public void SimulationPlan_CreateRawPassThrough_AllowsNullDocumentAndEmptyRules()
        {
            SimulationPlan plan = SimulationPlan.CreateRawPassThrough();
            Assert.IsNull(plan.Document);
            Assert.AreEqual(0, plan.MessageRules.Count);
        }

        [TestMethod]
        public async Task StartBaselineGatewayAsync_WithoutDbc_StartsRawBridge_AndAttachesDbcDynamically()
        {
            var driver = new MockHardwareService();
            var discovery = await driver.DiscoverInterfacesAsync();
            var iface = discovery.Value![0];
            var options = CanGatewayOptions.CreateClassic(iface.Channels[0], iface.Channels[1], 500000);
            var sessionResult = await driver.OpenGatewaySessionAsync(options);
            await using ICanGatewaySession session = sessionResult.Value!;

            using var vm = new SimulationViewModel();
            vm.SetSessionProvider(() => session);

            Assert.IsFalse(vm.HasDocument);
            Assert.IsFalse(vm.IsRunning);
            Assert.IsFalse(vm.CanStartInjection);

            // Start gateway without any DBC loaded
            await vm.StartBaselineGatewayAsync();

            Assert.IsTrue(vm.IsRunning);
            Assert.IsFalse(vm.HasDocument);
            Assert.IsFalse(vm.CanStartInjection); // Cannot inject without DBC

            // Load DBC dynamically while gateway bridge is actively running
            DbcDocument doc = ParseTestDocument();
            vm.LoadDocument(doc);

            Assert.IsTrue(vm.HasDocument);
            Assert.IsTrue(vm.IsRunning); // Gateway continues running!
            Assert.IsTrue(vm.CanStartInjection);

            // Unload DBC while gateway bridge is actively running
            vm.ClearDocument();

            Assert.IsFalse(vm.HasDocument);
            Assert.IsTrue(vm.IsRunning); // Gateway STILL continues running!
            Assert.IsFalse(vm.CanStartInjection);

            // Disconnect stops the gateway
            await vm.StopGatewayAsync();
            Assert.IsFalse(vm.IsRunning);
        }

        [TestMethod]
        public async Task SimulationEngine_RawPassThrough_RoutesFrameWithoutDbc()
        {
            var driver = new MockHardwareService();
            var discovery = await driver.DiscoverInterfacesAsync();
            var iface = discovery.Value![0];
            var options = CanGatewayOptions.CreateClassic(iface.Channels[0], iface.Channels[1], 500000);
            var sessionResult = await driver.OpenGatewaySessionAsync(options);
            var session = (MockCanGatewaySession)sessionResult.Value!;

            SimulationPlan plan = SimulationPlan.CreateRawPassThrough();
            await using var engine = new SimulationEngine(session, plan);

            await engine.StartAsync();
            Assert.IsTrue(engine.IsRunning);

            // Enqueue raw frame from RX
            var frame = CanFrame.CreateClassic(0x52C, false, [0x2C, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);
            await session.EnqueueReceivedAsync(CanGatewaySide.Rx, frame);

            // Read routed frame from TX
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            RoutedCanFrame outbound = default!;
            await foreach (RoutedCanFrame item in session.ReceiveTransmittedAsync(cts.Token))
            {
                outbound = item;
                break;
            }

            Assert.AreEqual(CanGatewaySide.Tx, outbound.Source);
            Assert.AreEqual(0x52Cu, outbound.Frame.Identifier);
            Assert.AreEqual(0x2C, outbound.Frame.Data.Span[0]);
            Assert.AreEqual(0x10, outbound.Frame.Data.Span[1]);

            await engine.StopAsync();
        }

        private static DbcDocument ParseTestDocument()
        {
            const string text = """
                BO_ 291 VehicleData: 2 Gateway
                 SG_ VehicleSpeed : 0|8@1+ (0.5,0) [0|127.5] "km/h" Gateway
                """;
            return DbcParser.Parse(text).Document
                ?? throw new InvalidOperationException("Failed to parse fixture.");
        }

        private sealed class FakeEngine : ISimulationEngine
        {
            public bool IsRunning { get; set; }
            public bool IsScheduling { get; set; }
            public bool IsSchedulingPaused { get; set; }
            public GatewayStatistics Statistics => GatewayStatistics.Empty;
            public HardwareFailure? LastFailure => null;

            public void ReplaceSignalOverrides(uint canId, bool isExtended, IEnumerable<SignalOverride> overrides)
            {
            }

#pragma warning disable CS0067
            public event Action<RoutedCanFrame>? FrameRouted;
#pragma warning restore CS0067

            public void UpdatePlan(SimulationPlan plan)
            {
            }

            public ValueTask StartAsync(CancellationToken cancellationToken = default)
            {
                IsRunning = true;
                return ValueTask.CompletedTask;
            }

            public ValueTask StartSchedulingAsync(CancellationToken cancellationToken = default)
            {
                IsScheduling = true;
                return ValueTask.CompletedTask;
            }

            public void PauseScheduling()
            {
                IsSchedulingPaused = true;
            }

            public void ResumeScheduling()
            {
                IsSchedulingPaused = false;
            }

            public ValueTask StopSchedulingAsync()
            {
                IsScheduling = false;
                IsSchedulingPaused = false;
                return ValueTask.CompletedTask;
            }

            public ValueTask<SimulationEventTriggerResult> TriggerEventAsync(uint canId, bool isExtended, CancellationToken token = default)
            {
                return ValueTask.FromResult(SimulationEventTriggerResult.Transmitted);
            }

            public ValueTask StopAsync()
            {
                IsRunning = false;
                IsScheduling = false;
                IsSchedulingPaused = false;
                return ValueTask.CompletedTask;
            }

            public ValueTask<HardwareOperationResult> EmergencyStopAsync()
            {
                return ValueTask.FromResult(HardwareOperationResult.Succeeded());
            }

            public ValueTask DisposeAsync()
            {
                IsRunning = false;
                return ValueTask.CompletedTask;
            }
        }
    }
}
