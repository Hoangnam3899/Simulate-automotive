using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;
using Simulate.ViewModels;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class LiveSignalMonitorTests
    {
        private const string SampleDbc = @"
VERSION ""1.0""

NS_ :

BS_:

BU_: NodeA NodeB

BO_ 256 EngineData: 8 NodeA
 SG_ EngineSpeed : 0|16@1+ (0.125,0) [0|8000] ""rpm"" NodeB
 SG_ EngineTemp : 16|8@1+ (1,-40) [-40|215] ""degC"" NodeB

BO_ 512 VehicleSpeed: 4 NodeA
 SG_ Speed : 0|16@1+ (0.01,0) [0|250] ""km/h"" NodeB

BO_ 768 MotorolaMessage: 8 NodeA
 SG_ BigEndianSignal : 7|16@0+ (1,0) [0|65535] ""V"" NodeB
";

        private static DbcDocument CreateSampleDocument()
        {
            DbcParseResult result = DbcParser.Parse(SampleDbc);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Document);
            return result.Document;
        }

        [TestMethod]
        public void Initial_state_signals_show_NoData_with_gray_color()
        {
            var viewModel = new SimulationViewModel();
            DbcDocument doc = CreateSampleDocument();
            viewModel.LoadDocument(doc);
            viewModel.AddMessages(doc.Messages);

            Assert.AreEqual(4, viewModel.Signals.Count);

            foreach (var signal in viewModel.Signals)
            {
                Assert.IsFalse(signal.HasReceivedData);
                Assert.AreEqual("● No Data", signal.StatusText);
                Assert.AreEqual("#64748B", signal.StatusColor);
                Assert.AreEqual("—", signal.RawValue);
                Assert.AreEqual("—", signal.PhysicalValueDisplay);
                Assert.AreEqual("—", signal.LastUpdated);
            }
        }

        [TestMethod]
        public void ProcessIncomingFrame_decodes_Intel_signals_and_updates_status_to_green()
        {
            var viewModel = new SimulationViewModel();
            DbcDocument doc = CreateSampleDocument();
            viewModel.LoadDocument(doc);
            viewModel.AddMessage(doc.Messages[0]); // EngineData (0x100)

            // Payload: EngineSpeed = 1000 raw (0x03E8) -> 125 rpm. (bytes 0,1: E8 03)
            // EngineTemp = 100 raw -> 60 degC (byte 2: 64)
            byte[] payload = new byte[] { 0xE8, 0x03, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00 };
            DateTime testTime = new DateTime(2026, 8, 20, 14, 30, 0, 500);

            viewModel.ProcessIncomingFrame(0x100, false, payload, testTime);

            SignalModel speedSig = viewModel.Signals.First(s => s.Name == "EngineSpeed");
            Assert.IsTrue(speedSig.HasReceivedData);
            Assert.AreEqual("● Active", speedSig.StatusText);
            Assert.AreEqual("#10B981", speedSig.StatusColor);
            Assert.AreEqual("0x3E8", speedSig.RawValue);
            Assert.AreEqual(125.0, speedSig.Value, 0.001);
            Assert.AreEqual("125 rpm", speedSig.PhysicalValueDisplay);
            Assert.AreEqual("14:30:00.500", speedSig.LastUpdated);

            SignalModel tempSig = viewModel.Signals.First(s => s.Name == "EngineTemp");
            Assert.IsTrue(tempSig.HasReceivedData);
            Assert.AreEqual("● Active", tempSig.StatusText);
            Assert.AreEqual("#10B981", tempSig.StatusColor);
            Assert.AreEqual("0x64", tempSig.RawValue);
            Assert.AreEqual(60.0, tempSig.Value, 0.001);
            Assert.AreEqual("60 degC", tempSig.PhysicalValueDisplay);
        }

        [TestMethod]
        public void ProcessIncomingFrame_decodes_Motorola_signals_correctly()
        {
            var viewModel = new SimulationViewModel();
            DbcDocument doc = CreateSampleDocument();
            viewModel.LoadDocument(doc);
            viewModel.AddMessage(doc.Messages[2]); // MotorolaMessage (0x300)

            // BigEndianSignal: 7|16@0+ -> Byte 0 is MSB, Byte 1 is LSB.
            // 0x12 0x34 -> raw = 0x1234 (4660)
            byte[] payload = new byte[] { 0x12, 0x34, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            DateTime testTime = new DateTime(2026, 8, 20, 14, 35, 10, 123);

            viewModel.ProcessIncomingFrame(0x300, false, payload, testTime);

            SignalModel sig = viewModel.Signals.First(s => s.Name == "BigEndianSignal");
            Assert.IsTrue(sig.HasReceivedData);
            Assert.AreEqual("● Active", sig.StatusText);
            Assert.AreEqual("#10B981", sig.StatusColor);
            Assert.AreEqual("0x1234", sig.RawValue);
            Assert.AreEqual(4660.0, sig.Value, 0.001);
            Assert.AreEqual("4660 V", sig.PhysicalValueDisplay);
        }

        [TestMethod]
        public void ProcessIncomingFrame_does_not_update_when_paused()
        {
            var viewModel = new SimulationViewModel();
            DbcDocument doc = CreateSampleDocument();
            viewModel.LoadDocument(doc);
            viewModel.AddMessage(doc.Messages[0]);

            viewModel.IsSignalMonitorPaused = true;
            Assert.AreEqual("▶", viewModel.PauseMonitorButtonContent);

            byte[] payload = new byte[] { 0xE8, 0x03, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00 };
            viewModel.ProcessIncomingFrame(0x100, false, payload);

            SignalModel speedSig = viewModel.Signals.First(s => s.Name == "EngineSpeed");
            Assert.IsFalse(speedSig.HasReceivedData);
            Assert.AreEqual("● No Data", speedSig.StatusText);
            Assert.AreEqual("#64748B", speedSig.StatusColor);
            Assert.AreEqual("—", speedSig.RawValue);
        }

        [TestMethod]
        public void ClearSignalMonitor_resets_all_signals_to_gray_NoData()
        {
            var viewModel = new SimulationViewModel();
            DbcDocument doc = CreateSampleDocument();
            viewModel.LoadDocument(doc);
            viewModel.AddMessage(doc.Messages[0]);

            byte[] payload = new byte[] { 0xE8, 0x03, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00 };
            viewModel.ProcessIncomingFrame(0x100, false, payload);

            SignalModel speedSig = viewModel.Signals.First(s => s.Name == "EngineSpeed");
            Assert.IsTrue(speedSig.HasReceivedData);

            viewModel.ClearSignalMonitorCommand.Execute(null);

            Assert.IsFalse(speedSig.HasReceivedData);
            Assert.AreEqual("● No Data", speedSig.StatusText);
            Assert.AreEqual("#64748B", speedSig.StatusColor);
            Assert.AreEqual("—", speedSig.RawValue);
            Assert.AreEqual("—", speedSig.PhysicalValueDisplay);
            Assert.AreEqual("—", speedSig.LastUpdated);
        }

        [TestMethod]
        public void TogglePauseMonitorCommand_toggles_pause_state_and_button_content()
        {
            var viewModel = new SimulationViewModel();

            Assert.IsFalse(viewModel.IsSignalMonitorPaused);
            Assert.AreEqual("Ⅱ", viewModel.PauseMonitorButtonContent);

            viewModel.TogglePauseMonitorCommand.Execute(null);

            Assert.IsTrue(viewModel.IsSignalMonitorPaused);
            Assert.AreEqual("▶", viewModel.PauseMonitorButtonContent);

            viewModel.TogglePauseMonitorCommand.Execute(null);

            Assert.IsFalse(viewModel.IsSignalMonitorPaused);
            Assert.AreEqual("Ⅱ", viewModel.PauseMonitorButtonContent);
        }

        [TestMethod]
        public void AvailableSignalMessageFilters_updates_dynamically_with_messages()
        {
            var viewModel = new SimulationViewModel();
            DbcDocument doc = CreateSampleDocument();
            viewModel.LoadDocument(doc);

            Assert.AreEqual(1, viewModel.AvailableSignalMessageFilters.Count);
            Assert.AreEqual("All Messages", viewModel.AvailableSignalMessageFilters[0]);

            viewModel.AddMessage(doc.Messages[0]); // EngineData

            Assert.AreEqual(2, viewModel.AvailableSignalMessageFilters.Count);
            Assert.AreEqual("All Messages", viewModel.AvailableSignalMessageFilters[0]);
            Assert.AreEqual("EngineData", viewModel.AvailableSignalMessageFilters[1]);

            viewModel.AddMessage(doc.Messages[1]); // VehicleSpeed

            Assert.AreEqual(3, viewModel.AvailableSignalMessageFilters.Count);
            Assert.IsTrue(viewModel.AvailableSignalMessageFilters.Contains("EngineData"));
            Assert.IsTrue(viewModel.AvailableSignalMessageFilters.Contains("VehicleSpeed"));

            viewModel.DeleteAllMessages();

            Assert.AreEqual(1, viewModel.AvailableSignalMessageFilters.Count);
            Assert.AreEqual("All Messages", viewModel.AvailableSignalMessageFilters[0]);
        }
    }
}
