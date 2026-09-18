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
        public void TogglingIsOverridden_UpdatesStatusAndDisplay()
        {
            var vm = CreateConfiguredViewModel(out _);
            var signal = vm.Signals.First(s => s.Name == "EngineSpeed");
            signal.Value = 3500;

            signal.IsOverridden = true;
            Assert.AreEqual("● Injected", signal.StatusText);
            Assert.AreEqual("#EF4444", signal.StatusColor);
            Assert.AreEqual("3500 rpm", signal.PhysicalValueDisplay);

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
    }
}
