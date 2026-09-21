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
    public class SignalValueConfigurationTests
    {
        private class TestEngine : ISimulationEngine
        {
            public bool IsRunning => true;
            public bool IsScheduling => false;
            public bool IsSchedulingPaused => false;
            public GatewayStatistics Statistics => GatewayStatistics.Empty;
            public HardwareFailure? LastFailure => null;

            public uint LastCanId { get; private set; }
            public bool LastIsExtended { get; private set; }
            public List<SignalOverride>? LastOverrides { get; private set; }
            public int ReplaceOverridesCallCount { get; private set; }

            public void ReplaceSignalOverrides(
                uint canIdentifier,
                bool isExtendedIdentifier,
                IEnumerable<SignalOverride> signalOverrides)
            {
                LastCanId = canIdentifier;
                LastIsExtended = isExtendedIdentifier;
                LastOverrides = signalOverrides.ToList();
                ReplaceOverridesCallCount++;
            }

#pragma warning disable CS0067
            public event Action<RoutedCanFrame>? FrameRouted;
#pragma warning restore CS0067

            public void UpdatePlan(SimulationPlan plan) { }

            public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
            public ValueTask StartSchedulingAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
            public void PauseScheduling() { }
            public void ResumeScheduling() { }
            public ValueTask StopSchedulingAsync() => ValueTask.CompletedTask;
            public ValueTask StopAsync() => ValueTask.CompletedTask;
            public ValueTask<HardwareOperationResult> EmergencyStopAsync() => ValueTask.FromResult(HardwareOperationResult.Succeeded());
            public ValueTask<SimulationEventTriggerResult> TriggerEventAsync(
                uint canIdentifier,
                bool isExtendedIdentifier,
                CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(SimulationEventTriggerResult.Transmitted);
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private static SimulationViewModel CreateConfiguredViewModel(out TestEngine engine)
        {
            const string documentText = """
                BO_ 291 EngineData: 8 Gateway
                 SG_ EngineSpeed : 0|16@1+ (0.25,0) [0|8000] "rpm" Gateway
                 SG_ EngineTemp : 16|8@1+ (1,-40) [-40|215] "degC" Gateway
                BO_ 419 BrakeStatus: 4 Gateway
                 SG_ BrakeApplied : 0|1@1+ (1,0) [0|1] "" Gateway
                VAL_ 419 BrakeApplied 0 "Released" 1 "Applied" ;
                """;

            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new InvalidOperationException("Failed to parse fixture DBC");

            engine = new TestEngine();
            var plan = new SimulationPlan(document, Array.Empty<SimulationMessageRule>());
            return new SimulationViewModel(plan, engine);
        }

        [TestMethod]
        public void Default_FilteredValueSignals_ContainsAllSignals()
        {
            var vm = CreateConfiguredViewModel(out _);

            Assert.IsNotNull(vm.FilteredValueSignals);
            var list = vm.FilteredValueSignals.Cast<SignalModel>().ToList();
            Assert.AreEqual(3, list.Count);
            Assert.IsTrue(list.Any(s => s.Name == "EngineSpeed"));
            Assert.IsTrue(list.Any(s => s.Name == "EngineTemp"));
            Assert.IsTrue(list.Any(s => s.Name == "BrakeApplied"));
        }

        [TestMethod]
        public void SignalValueSearch_FiltersBySignalName()
        {
            var vm = CreateConfiguredViewModel(out _);

            vm.SignalValueSearchText = "Speed";
            var list = vm.FilteredValueSignals!.Cast<SignalModel>().ToList();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("EngineSpeed", list[0].Name);

            vm.SignalValueSearchText = "Engine";
            list = vm.FilteredValueSignals!.Cast<SignalModel>().ToList();
            Assert.AreEqual(2, list.Count);

            vm.SignalValueSearchText = "NonExistent";
            list = vm.FilteredValueSignals!.Cast<SignalModel>().ToList();
            Assert.AreEqual(0, list.Count);

            vm.SignalValueSearchText = string.Empty;
            list = vm.FilteredValueSignals!.Cast<SignalModel>().ToList();
            Assert.AreEqual(3, list.Count);
        }

        [TestMethod]
        public void SignalValueSearch_FiltersByMessageName()
        {
            var vm = CreateConfiguredViewModel(out _);

            vm.SignalValueSearchText = "BrakeStatus";
            var list = vm.FilteredValueSignals!.Cast<SignalModel>().ToList();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("BrakeApplied", list[0].Name);
        }

        [TestMethod]
        public void ShowOnlyOverridden_FiltersOutNonOverriddenSignals()
        {
            var vm = CreateConfiguredViewModel(out _);

            vm.ShowOnlyOverridden = true;
            var list = vm.FilteredValueSignals!.Cast<SignalModel>().ToList();
            Assert.AreEqual(0, list.Count);

            // Toggle one signal override
            var speedSignal = vm.Signals.First(s => s.Name == "EngineSpeed");
            speedSignal.IsOverridden = true;

            list = vm.FilteredValueSignals!.Cast<SignalModel>().ToList();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("EngineSpeed", list[0].Name);

            // Toggle off
            speedSignal.IsOverridden = false;
            list = vm.FilteredValueSignals!.Cast<SignalModel>().ToList();
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void TogglingIsOverridden_PreservesLiveStatusAndEnablesOverride()
        {
            var vm = CreateConfiguredViewModel(out _);
            var signal = vm.Signals.First(s => s.Name == "EngineSpeed");
            signal.Value = 3500;

            signal.IsOverridden = true;
            // Setup in UI 6 does not prematurely inject or mutate UI 4 live monitor status
            Assert.AreEqual("● No Data", signal.StatusText);
            Assert.AreEqual("#64748B", signal.StatusColor);
            Assert.AreEqual("—", signal.PhysicalValueDisplay);
            Assert.IsTrue(signal.IsOverridden);

            signal.IsOverridden = false;
            Assert.AreEqual("● No Data", signal.StatusText);
            Assert.AreEqual("#64748B", signal.StatusColor);
        }

        [TestMethod]
        public void OverrideSync_CallsEngineReplaceSignalOverrides()
        {
            var vm = CreateConfiguredViewModel(out var engine);
            var signal = vm.Signals.First(s => s.Name == "EngineSpeed");
            signal.Value = 2500;

            int initialCallCount = engine.ReplaceOverridesCallCount;

            // Enable override
            signal.IsOverridden = true;

            Assert.AreEqual(initialCallCount + 1, engine.ReplaceOverridesCallCount);
            Assert.AreEqual(291u, engine.LastCanId);
            Assert.IsFalse(engine.LastIsExtended);
            Assert.IsNotNull(engine.LastOverrides);
            Assert.AreEqual(1, engine.LastOverrides.Count);
            Assert.AreEqual("EngineSpeed", engine.LastOverrides[0].SignalName);
            Assert.AreEqual(2500d, engine.LastOverrides[0].PhysicalValue);

            // Edit value
            signal.Value = 3000;
            Assert.AreEqual(initialCallCount + 2, engine.ReplaceOverridesCallCount);
            Assert.AreEqual(3000d, engine.LastOverrides[0].PhysicalValue);

            // Disable override -> sends empty list to clear
            signal.IsOverridden = false;
            Assert.AreEqual(initialCallCount + 3, engine.ReplaceOverridesCallCount);
            Assert.AreEqual(0, engine.LastOverrides.Count);
        }

        [TestMethod]
        public void SelectingSignalInValuePanel_SyncsToFaultConfiguration()
        {
            var vm = CreateConfiguredViewModel(out _);
            var speedSignal = vm.Signals.First(s => s.Name == "EngineSpeed");

            vm.SelectedSignal = speedSignal;

            Assert.IsTrue(vm.FaultConfig.HasSelectedSignal);
            Assert.AreEqual("Selected: EngineData.EngineSpeed", vm.FaultConfig.SelectedSignalDisplay);
        }

        [TestMethod]
        public void SignalWithValueDescriptions_ExposesAvailableDescriptionsAndDynamicDisplay()
        {
            var vm = CreateConfiguredViewModel(out _);
            var brakeSignal = vm.Signals.First(s => s.Name == "BrakeApplied");

            Assert.IsTrue(brakeSignal.HasValueDescriptions);
            Assert.IsNotNull(brakeSignal.AvailableValueDescriptions);
            Assert.AreEqual(2, brakeSignal.AvailableValueDescriptions.Count);
            Assert.AreEqual("[0] Released", brakeSignal.AvailableValueDescriptions[0]);
            Assert.AreEqual("[1] Applied", brakeSignal.AvailableValueDescriptions[1]);
            Assert.AreEqual("[0] Released", brakeSignal.PhysicalValueInput);

            // Select "[1] Applied"
            brakeSignal.PhysicalValueInput = "[1] Applied";
            Assert.AreEqual(1d, brakeSignal.Value);
            Assert.IsTrue(brakeSignal.IsOverridden);
            Assert.AreEqual("—", brakeSignal.PhysicalValueDisplay);
            Assert.AreEqual("● No Data", brakeSignal.StatusText);
        }

        [TestMethod]
        public void ContinuousSignal_MinMaxValidation_UpdatesValueColor()
        {
            var vm = CreateConfiguredViewModel(out _);
            var speedSignal = vm.Signals.First(s => s.Name == "EngineSpeed");

            Assert.AreEqual(0d, speedSignal.Min);
            Assert.AreEqual(8000d, speedSignal.Max);

            // In range
            speedSignal.PhysicalValueInput = "5000";
            Assert.AreEqual(5000d, speedSignal.Value);
            Assert.IsTrue(speedSignal.IsValueValid);
            Assert.AreEqual("#F8FAFC", speedSignal.ValueColor);

            // Out of range (exceeds max 8000)
            speedSignal.PhysicalValueInput = "9000";
            Assert.AreEqual(9000d, speedSignal.Value);
            Assert.IsFalse(speedSignal.IsValueValid);
            Assert.AreEqual("#EF4444", speedSignal.ValueColor);

            // Negative (below min 0)
            speedSignal.PhysicalValueInput = "-50";
            Assert.AreEqual(-50d, speedSignal.Value);
            Assert.IsFalse(speedSignal.IsValueValid);
            Assert.AreEqual("#EF4444", speedSignal.ValueColor);
            StringAssert.Contains(speedSignal.ValidationToolTip, "CẢNH BÁO");
            StringAssert.Contains(speedSignal.ValidationToolTip, "vượt dải cho phép");

            // Valid again
            speedSignal.PhysicalValueInput = "2500";
            Assert.AreEqual(2500d, speedSignal.Value);
            Assert.IsTrue(speedSignal.IsValueValid);
            Assert.AreEqual("#F8FAFC", speedSignal.ValueColor);
            Assert.IsFalse(speedSignal.ValidationToolTip.Contains("CẢNH BÁO"));
        }

        [TestMethod]
        public void ClearAllOverrides_ResetsAllOverriddenSignals()
        {
            var vm = CreateConfiguredViewModel(out var engine);
            var speedSignal = vm.Signals.First(s => s.Name == "EngineSpeed");
            var tempSignal = vm.Signals.First(s => s.Name == "EngineTemp");

            speedSignal.IsOverridden = true;
            tempSignal.IsOverridden = true;

            Assert.AreEqual(2, vm.Signals.Count(s => s.IsOverridden));

            vm.ClearAllOverridesCommand.Execute(null);

            Assert.AreEqual(0, vm.Signals.Count(s => s.IsOverridden));
            Assert.IsFalse(speedSignal.IsOverridden);
            Assert.IsFalse(tempSignal.IsOverridden);
        }

        [TestMethod]
        public void SelectingMessage_InMessageList_FiltersLiveMonitorAndValueConfigToSelectedMessage()
        {
            var vm = CreateConfiguredViewModel(out _);
            var engineDataMsg = vm.Messages.First(m => m.Name == "EngineData");
            var brakeStatusMsg = vm.Messages.First(m => m.Name == "BrakeStatus");

            // Select BrakeStatus message in Bảng 3
            vm.SelectedMessage = brakeStatusMsg;

            // Bảng 6 (FilteredValueSignals) must only show signals of BrakeStatus
            var valList = vm.FilteredValueSignals!.Cast<SignalModel>().ToList();
            Assert.AreEqual(1, valList.Count);
            Assert.AreEqual("BrakeApplied", valList[0].Name);
            Assert.AreEqual("BrakeStatus", valList[0].MessageName);

            // Bảng 4 (FilteredSignals) must also only show signals of BrakeStatus
            var liveList = vm.FilteredSignals!.Cast<SignalModel>().ToList();
            Assert.AreEqual(1, liveList.Count);
            Assert.AreEqual("BrakeApplied", liveList[0].Name);
            Assert.AreEqual("BrakeStatus", liveList[0].MessageName);

            // Select EngineData message in Bảng 3
            vm.SelectedMessage = engineDataMsg;

            valList = vm.FilteredValueSignals!.Cast<SignalModel>().ToList();
            Assert.AreEqual(2, valList.Count);
            Assert.IsTrue(valList.All(s => s.MessageName == "EngineData"));

            liveList = vm.FilteredSignals!.Cast<SignalModel>().ToList();
            Assert.AreEqual(2, liveList.Count);
            Assert.IsTrue(liveList.All(s => s.MessageName == "EngineData"));
        }

        [TestMethod]
        public void ClearingSelectedMessage_RestoresAllSignalsInLiveMonitorAndValueConfig()
        {
            var vm = CreateConfiguredViewModel(out _);
            var brakeStatusMsg = vm.Messages.First(m => m.Name == "BrakeStatus");

            vm.SelectedMessage = brakeStatusMsg;
            Assert.AreEqual(1, vm.FilteredValueSignals!.Cast<SignalModel>().Count());
            Assert.AreEqual(1, vm.FilteredSignals!.Cast<SignalModel>().Count());

            // Clear selection (null)
            vm.SelectedMessage = null;

            // Restores all 3 signals in both views
            Assert.AreEqual(3, vm.FilteredValueSignals!.Cast<SignalModel>().Count());
            Assert.AreEqual(3, vm.FilteredSignals!.Cast<SignalModel>().Count());
        }

        [TestMethod]
        public void ChangingSelectedSignalMessageFilter_BidirectionallySyncsSelectedMessage()
        {
            var vm = CreateConfiguredViewModel(out _);

            // User selects a message in UI 4 ComboBox filter
            vm.SelectedSignalMessageFilter = "BrakeStatus";
            Assert.IsNotNull(vm.SelectedMessage);
            Assert.AreEqual("BrakeStatus", vm.SelectedMessage.Name);
            Assert.AreEqual(1, vm.FilteredValueSignals!.Cast<SignalModel>().Count());

            // User selects "All Messages" in UI 4 ComboBox filter
            vm.SelectedSignalMessageFilter = "All Messages";
            Assert.IsNull(vm.SelectedMessage);
            Assert.AreEqual(3, vm.FilteredValueSignals!.Cast<SignalModel>().Count());
        }

        [TestMethod]
        public async Task SignalValueEdit_WhenEngineInPassThroughMode_DoesNotCrashAndSavesOverride()
        {
            var driver = new MockHardwareService();
            var discovery = await driver.DiscoverInterfacesAsync();
            var iface = discovery.Value![0];
            var options = CanGatewayOptions.CreateClassic(iface.Channels[0], iface.Channels[1], 500000);
            var sessionResult = await driver.OpenGatewaySessionAsync(options);
            await using ICanGatewaySession session = sessionResult.Value!;

            const string documentText = """
                BO_ 291 EngineData: 8 Gateway
                 SG_ EngineSpeed : 0|16@1+ (0.25,0) [0|8000] "rpm" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document!;

            // Engine in baseline pass-through mode
            var plan = new SimulationPlan(document, [
                new SimulationMessageRule(291, false, true, GatewayMode.PassThrough, SimulationSendType.Cyclic,
                    new SimulationTiming(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), 0), [], new E2eProtectionConfiguration(false))
            ]);
            await using var engine = new SimulationEngine(session, plan);
            await engine.StartAsync();

            using var vm = new SimulationViewModel(plan, engine);

            // User edits signal in Panel 6 while engine is running in Pass-Through mode
            var signal = vm.Signals.First(s => s.Name == "EngineSpeed");

            // Must not throw ArgumentException!
            signal.Value = 2500;
            signal.IsOverridden = true;

            Assert.AreEqual(2500d, signal.Value);
            Assert.IsTrue(signal.IsOverridden);

            // Turn off override -> Must not throw
            signal.IsOverridden = false;
            Assert.IsFalse(signal.IsOverridden);

            await engine.StopAsync();
        }
    }
}
