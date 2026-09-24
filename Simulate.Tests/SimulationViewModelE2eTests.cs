using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;
using Simulate.ViewModels;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class SimulationViewModelE2eTests
    {
        private DbcDocument CreateTestDocument()
        {
            var e2eSignals = new List<DbcSignal>
            {
                new DbcSignal(
                    "VCU_01_CheckSum",
                    startBit: 7,
                    bitLength: 8,
                    byteOrder: DbcByteOrder.BigEndian,
                    isSigned: false,
                    factor: 1,
                    offset: 0,
                    minimum: 0,
                    maximum: 255,
                    unit: "",
                    receivers: [],
                    valueDescriptions: []),
                new DbcSignal(
                    "VCU_01_RollingCounter",
                    startBit: 11,
                    bitLength: 4,
                    byteOrder: DbcByteOrder.BigEndian,
                    isSigned: false,
                    factor: 1,
                    offset: 0,
                    minimum: 0,
                    maximum: 15,
                    unit: "",
                    receivers: [],
                    valueDescriptions: []),
                new DbcSignal(
                    "VCU_Gear",
                    startBit: 18,
                    bitLength: 3,
                    byteOrder: DbcByteOrder.BigEndian,
                    isSigned: false,
                    factor: 1,
                    offset: 0,
                    minimum: 0,
                    maximum: 7,
                    unit: "",
                    receivers: [],
                    valueDescriptions: [])
            };

            var nonE2eSignals = new List<DbcSignal>
            {
                new DbcSignal(
                    "Speed",
                    startBit: 0,
                    bitLength: 16,
                    byteOrder: DbcByteOrder.LittleEndian,
                    isSigned: false,
                    factor: 0.1,
                    offset: 0,
                    minimum: 0,
                    maximum: 200,
                    unit: "km/h",
                    receivers: [],
                    valueDescriptions: [])
            };

            var messages = new List<DbcMessage>
            {
                new DbcMessage("VCU_01", 0x100, isExtendedIdentifier: false, payloadLength: 8, transmitter: "VCU", signals: e2eSignals),
                new DbcMessage("Meter", 0x200, isExtendedIdentifier: false, payloadLength: 8, transmitter: "IPC", signals: nonE2eSignals)
            };

            return new DbcDocument(new List<DbcNode>(), messages);
        }

        [TestMethod]
        public void SelectedSignal_WhenMessageHasE2e_SetsCheckedAndAutoStatus()
        {
            var doc = CreateTestDocument();
            var vm = new SimulationViewModel();
            vm.LoadDocument(doc);
            vm.AddMessages(doc.Messages); // Populate TX messages and signals

            // Find signal belonging to VCU_01
            var signal = vm.Signals[2]; // VCU_Gear
            Assert.AreEqual("VCU_Gear", signal.Name);

            vm.SelectedSignal = signal;

            Assert.IsTrue(vm.CanToggleSelectedSignalE2e);
            Assert.IsTrue(vm.IsSelectedSignalE2eChecked);
            Assert.AreEqual("● E2E: Active (Auto CRC8)", vm.SelectedSignalE2eText);
            Assert.AreEqual("#10B981", vm.SelectedSignalE2eColor);
        }

        [TestMethod]
        public void Checkbox_WhenUserUnticks_DisablesE2eAndSetsInactiveStatus()
        {
            var doc = CreateTestDocument();
            var vm = new SimulationViewModel();
            vm.LoadDocument(doc);
            vm.AddMessages(doc.Messages);

            var signal = vm.Signals[2]; // VCU_Gear
            vm.SelectedSignal = signal;
            Assert.IsTrue(vm.IsSelectedSignalE2eChecked);

            // User unticks
            vm.IsSelectedSignalE2eChecked = false;

            Assert.IsFalse(vm.IsSelectedSignalE2eChecked);
            Assert.AreEqual("○ E2E: Inactive", vm.SelectedSignalE2eText);
            Assert.AreEqual("#64748B", vm.SelectedSignalE2eColor);

            // Verify in plan
            var plan = vm.BuildSimulationPlan();
            var rule = plan.MessageRules[0];
            Assert.IsFalse(rule.E2eProtection.IsEnabled);
        }

        [TestMethod]
        public void SelectedSignal_WhenMessageHasNoE2e_SetsUncheckedAndInactiveStatus()
        {
            var doc = CreateTestDocument();
            var vm = new SimulationViewModel();
            vm.LoadDocument(doc);
            vm.AddMessages(doc.Messages);

            var speedSignal = vm.Signals[3]; // Speed in Meter
            Assert.AreEqual("Speed", speedSignal.Name);

            vm.SelectedSignal = speedSignal;

            Assert.IsTrue(vm.CanToggleSelectedSignalE2e);
            Assert.IsFalse(vm.IsSelectedSignalE2eChecked);
            Assert.AreEqual("○ E2E: Inactive", vm.SelectedSignalE2eText);
            Assert.AreEqual("#64748B", vm.SelectedSignalE2eColor);
        }

        [TestMethod]
        public void Checkbox_WhenUserTicksOnNonE2eMessage_ActivatesManualFallback()
        {
            var doc = CreateTestDocument();
            var vm = new SimulationViewModel();
            vm.LoadDocument(doc);
            vm.AddMessages(doc.Messages);

            var speedSignal = vm.Signals[3]; // Speed in Meter
            vm.SelectedSignal = speedSignal;
            Assert.IsFalse(vm.IsSelectedSignalE2eChecked);

            // User forces E2E on
            vm.IsSelectedSignalE2eChecked = true;

            Assert.IsTrue(vm.IsSelectedSignalE2eChecked);
            Assert.AreEqual("● E2E: Active (Manual)", vm.SelectedSignalE2eText);
            Assert.AreEqual("#06B6D4", vm.SelectedSignalE2eColor);

            var plan = vm.BuildSimulationPlan();
            var rule = plan.MessageRules[1]; // Meter message
            Assert.IsTrue(rule.E2eProtection.IsEnabled);
            Assert.AreEqual(0, rule.E2eProtection.ChecksumByteIndex);
            Assert.AreEqual(1, rule.E2eProtection.CounterByteIndex);
        }
    }
}
