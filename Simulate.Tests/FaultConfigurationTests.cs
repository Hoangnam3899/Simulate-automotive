using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.ViewModels;

namespace Simulate.Tests
{
    [TestClass]
    public class FaultConfigurationTests
    {
        [TestMethod]
        public void DefaultProperties_AreInitializedCorrectly()
        {
            var queue = new ObservableCollection<FaultQueueModel>();
            var vm = new FaultConfigurationViewModel(queue);

            Assert.AreEqual("Selected: (No signal selected)", vm.SelectedSignalDisplay);
            Assert.IsFalse(vm.HasSelectedSignal);
            Assert.AreEqual("Signal Override", vm.SelectedFaultType);
            Assert.AreEqual("Cyclic", vm.SelectedInjectionMode);
            Assert.AreEqual(Visibility.Collapsed, vm.AddToQueueVisibility);
            Assert.IsTrue(vm.AvailableInjectionModes.Contains("Event"));
            Assert.IsTrue(vm.AvailableFaultTypes.Contains("Signal Override"));
            Assert.AreEqual("100 ms", vm.CycleTimeText);
            Assert.AreEqual("0 ms", vm.StartDelayText);
            Assert.AreEqual("0", vm.RepeatCountText);
            Assert.AreEqual("10 s", vm.DurationText);
            Assert.AreEqual("0 s", vm.StopTimeText);
            Assert.AreEqual("0x0000", vm.FaultValueText);
            Assert.IsTrue(vm.IsOverrideExisting);
            Assert.IsTrue(vm.IsRestoreAfterStop);
            Assert.IsFalse(vm.AddToQueueCommand.CanExecute(null));
            Assert.IsTrue(vm.ModeGuideText.Contains("Cyclic: Repeated transmission"));
            Assert.IsTrue(vm.FaultTypeGuideText.Contains("Signal Override: Replaces live signal data"));
            Assert.IsTrue(vm.CombinedGuideText.Contains("Signal Override:"));
            Assert.IsTrue(vm.CombinedGuideText.Contains("Cyclic:"));
        }

        [TestMethod]
        public void FaultTypeGuide_SwitchesCorrectlyAcrossAllFaultTypes()
        {
            var queue = new ObservableCollection<FaultQueueModel>();
            var vm = new FaultConfigurationViewModel(queue);

            vm.SelectedFaultType = "Signal Override";
            Assert.IsTrue(vm.FaultTypeGuideText.Contains("Signal Override:"));

            vm.SelectedFaultType = "Stuck at Value";
            Assert.IsTrue(vm.FaultTypeGuideText.Contains("Stuck at Value:"));

            vm.SelectedFaultType = "Bit Flip";
            Assert.IsTrue(vm.FaultTypeGuideText.Contains("Bit Flip:"));

            vm.SelectedFaultType = "Offset";
            Assert.IsTrue(vm.FaultTypeGuideText.Contains("Offset:"));

            vm.SelectedFaultType = "Noise";
            Assert.IsTrue(vm.FaultTypeGuideText.Contains("Noise:"));

            vm.SelectedFaultType = "Ramp";
            Assert.IsTrue(vm.FaultTypeGuideText.Contains("Ramp:"));

            Assert.IsTrue(vm.CombinedGuideText.Contains("Ramp:"));
            Assert.IsTrue(vm.CombinedGuideText.Contains("Cyclic:"));
        }

        [TestMethod]
        public void AddToQueueVisibility_AndModeGuide_SwitchesCorrectlyAcrossAllModes()
        {
            var queue = new ObservableCollection<FaultQueueModel>();
            var vm = new FaultConfigurationViewModel(queue);

            vm.SelectedInjectionMode = "Cyclic";
            Assert.AreEqual(Visibility.Collapsed, vm.AddToQueueVisibility);
            Assert.IsTrue(vm.ModeGuideText.Contains("Cyclic:"));

            vm.SelectedInjectionMode = "One-Shot";
            Assert.AreEqual(Visibility.Collapsed, vm.AddToQueueVisibility);
            Assert.IsTrue(vm.ModeGuideText.Contains("One-Shot:"));

            vm.SelectedInjectionMode = "Event";
            Assert.AreEqual(Visibility.Collapsed, vm.AddToQueueVisibility);
            Assert.IsTrue(vm.ModeGuideText.Contains("Event:"));

            vm.SelectedInjectionMode = "Sequence";
            Assert.AreEqual(Visibility.Visible, vm.AddToQueueVisibility);
            Assert.IsTrue(vm.ModeGuideText.Contains("Sequence:"));

            vm.SelectedInjectionMode = "Cyclic";
            Assert.AreEqual(Visibility.Collapsed, vm.AddToQueueVisibility);
            Assert.IsTrue(vm.ModeGuideText.Contains("Cyclic:"));
        }

        [TestMethod]
        public void FieldEnablement_SwitchesCorrectlyAcrossAllModes()
        {
            var queue = new ObservableCollection<FaultQueueModel>();
            var vm = new FaultConfigurationViewModel(queue);

            // 1. Cyclic: all enabled
            vm.SelectedInjectionMode = "Cyclic";
            Assert.IsTrue(vm.IsCycleEnabled);
            Assert.IsTrue(vm.IsRepeatEnabled);
            Assert.IsTrue(vm.IsDurationEnabled);
            Assert.IsTrue(vm.IsDelayEnabled);

            // 2. One-Shot: only Delay is enabled
            vm.SelectedInjectionMode = "One-Shot";
            Assert.IsFalse(vm.IsCycleEnabled);
            Assert.IsFalse(vm.IsRepeatEnabled);
            Assert.IsFalse(vm.IsDurationEnabled);
            Assert.IsTrue(vm.IsDelayEnabled);

            // 3. Event: all disabled
            vm.SelectedInjectionMode = "Event";
            Assert.IsFalse(vm.IsCycleEnabled);
            Assert.IsFalse(vm.IsRepeatEnabled);
            Assert.IsFalse(vm.IsDurationEnabled);
            Assert.IsFalse(vm.IsDelayEnabled);

            // 4. Sequence: all enabled
            vm.SelectedInjectionMode = "Sequence";
            Assert.IsTrue(vm.IsCycleEnabled);
            Assert.IsTrue(vm.IsRepeatEnabled);
            Assert.IsTrue(vm.IsDurationEnabled);
            Assert.IsTrue(vm.IsDelayEnabled);
        }

        [TestMethod]
        public void SetTargetSignal_UpdatesDisplayAndAutoPopulatesFaultValue()
        {
            var queue = new ObservableCollection<FaultQueueModel>();
            var vm = new FaultConfigurationViewModel(queue);

            var signal = new SignalModel
            {
                Name = "EngineSpeed",
                MessageName = "EngineData",
                MessageId = "0x100",
                Value = 2500.5
            };

            vm.SetTargetSignal(signal);

            Assert.IsTrue(vm.HasSelectedSignal);
            Assert.AreEqual("Selected: EngineData.EngineSpeed", vm.SelectedSignalDisplay);
            Assert.AreEqual("2500.5", vm.FaultValueText);
            Assert.IsTrue(vm.AddToQueueCommand.CanExecute(null));

            vm.SetTargetSignal(null);
            Assert.IsFalse(vm.HasSelectedSignal);
            Assert.AreEqual("Selected: (No signal selected)", vm.SelectedSignalDisplay);
            Assert.IsFalse(vm.AddToQueueCommand.CanExecute(null));
        }

        [TestMethod]
        public void AddToQueue_AddsItemWithConfiguredParameters()
        {
            var queue = new ObservableCollection<FaultQueueModel>();
            var vm = new FaultConfigurationViewModel(queue);

            var signal = new SignalModel
            {
                Name = "WheelSpeed_FL",
                MessageName = "BrakeStatus",
                MessageId = "0x200"
            };

            vm.SetTargetSignal(signal);
            vm.SelectedFaultType = "Bit Flip";
            vm.SelectedInjectionMode = "Sequence";
            vm.FaultValueText = "Bit 3";
            vm.DurationText = "5 s";
            vm.StartDelayText = "200 ms";
            vm.RepeatCountText = "10";

            vm.AddToQueueCommand.Execute(null);

            Assert.AreEqual(1, queue.Count);
            var item = queue[0];
            Assert.AreEqual(1, item.Index);
            Assert.AreEqual("0x200", item.MsgId);
            Assert.AreEqual("BrakeStatus", item.MsgName);
            Assert.AreEqual("WheelSpeed_FL", item.Signal);
            Assert.AreEqual("Bit Flip", item.FaultType);
            Assert.AreEqual("Bit 3", item.FaultValue);
            Assert.AreEqual("5 s", item.Duration);
            Assert.AreEqual("200 ms", item.StartTime);
            Assert.AreEqual("10", item.Repeat);
            Assert.AreEqual("Sequence", item.Mode);
            Assert.AreEqual("Queued", item.Status);
        }

        [TestMethod]
        public void SimulationViewModel_SelectedSignalChanged_UpdatesFaultConfig()
        {
            var simVm = new SimulationViewModel();
            Assert.IsNotNull(simVm.FaultConfig);
            Assert.AreEqual("Selected: (No signal selected)", simVm.FaultConfig.SelectedSignalDisplay);

            var signal = new SignalModel
            {
                Name = "VehicleSpeed",
                MessageName = "DashboardData",
                MessageId = "0x350",
                Value = 85.0
            };

            simVm.SelectedSignal = signal;

            Assert.AreEqual("Selected: DashboardData.VehicleSpeed", simVm.FaultConfig.SelectedSignalDisplay);
            Assert.IsTrue(simVm.FaultConfig.HasSelectedSignal);
            Assert.AreEqual("85", simVm.FaultConfig.FaultValueText);
        }
    }
}
