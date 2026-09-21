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

        [TestMethod]
        public async System.Threading.Tasks.Task LiveSignalMonitor_WhenFrameReceivedFromTxSide_UpdatesSignalsCorrectly()
        {
            var driver = new MockHardwareService();
            var discovery = await driver.DiscoverInterfacesAsync();
            var iface = discovery.Value![0];
            var options = CanGatewayOptions.CreateClassic(iface.Channels[0], iface.Channels[1], 500000);
            var sessionResult = await driver.OpenGatewaySessionAsync(options);
            var mockSession = (MockCanGatewaySession)sessionResult.Value!;
            await using ICanGatewaySession session = mockSession;

            DbcDocument doc = CreateSampleDocument();
            var plan = new SimulationPlan(doc, [
                new SimulationMessageRule(256, false, true, GatewayMode.PassThrough, SimulationSendType.Cyclic,
                    new SimulationTiming(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), 0), [], new E2eProtectionConfiguration(false))
            ]);
            await using var engine = new SimulationEngine(session, plan);
            await engine.StartAsync();

            using var viewModel = new SimulationViewModel(plan, engine);

            // Frame coming from TX side (Virtual Bus 1)
            byte[] payload = new byte[] { 0xE8, 0x03, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var frame = CanFrame.CreateClassic(256, false, payload);
            await mockSession.EnqueueReceivedAsync(CanGatewaySide.Tx, frame);

            // Allow short time for engine receive loop to route frame
            await System.Threading.Tasks.Task.Delay(100);

            // Flush live buffer to signals
            viewModel.FlushLiveBufferToSignals();

            SignalModel speedSig = viewModel.Signals.First(s => s.Name == "EngineSpeed");
            Assert.IsTrue(speedSig.HasReceivedData, "Signal must receive data even when frame source is TX");
            Assert.AreEqual("● Active", speedSig.StatusText);
            Assert.AreEqual("#10B981", speedSig.StatusColor);
            Assert.AreEqual("0x3E8", speedSig.RawValue);
            Assert.AreEqual(125.0, speedSig.Value, 0.001);

            await engine.StopAsync();
        }

        [TestMethod]
        public async System.Threading.Tasks.Task LiveSignalMonitor_WhenFrameReceivedFromRxSide_UpdatesSignalsCorrectly()
        {
            var driver = new MockHardwareService();
            var discovery = await driver.DiscoverInterfacesAsync();
            var iface = discovery.Value![0];
            var options = CanGatewayOptions.CreateClassic(iface.Channels[0], iface.Channels[1], 500000);
            var sessionResult = await driver.OpenGatewaySessionAsync(options);
            var mockSession = (MockCanGatewaySession)sessionResult.Value!;
            await using ICanGatewaySession session = mockSession;

            DbcDocument doc = CreateSampleDocument();
            var plan = new SimulationPlan(doc, [
                new SimulationMessageRule(256, false, true, GatewayMode.PassThrough, SimulationSendType.Cyclic,
                    new SimulationTiming(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), 0), [], new E2eProtectionConfiguration(false))
            ]);
            await using var engine = new SimulationEngine(session, plan);
            await engine.StartAsync();

            using var viewModel = new SimulationViewModel(plan, engine);

            // Frame coming from RX side (Virtual Bus 2)
            byte[] payload = new byte[] { 0xE8, 0x03, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var frame = CanFrame.CreateClassic(256, false, payload);
            await mockSession.EnqueueReceivedAsync(CanGatewaySide.Rx, frame);

            // Allow short time for engine receive loop to route frame
            await System.Threading.Tasks.Task.Delay(100);

            viewModel.FlushLiveBufferToSignals();

            SignalModel speedSig = viewModel.Signals.First(s => s.Name == "EngineSpeed");
            Assert.IsTrue(speedSig.HasReceivedData, "Signal must receive data when frame source is RX");
            Assert.AreEqual("● Active", speedSig.StatusText);
            Assert.AreEqual("#10B981", speedSig.StatusColor);
            Assert.AreEqual("0x3E8", speedSig.RawValue);
            Assert.AreEqual(125.0, speedSig.Value, 0.001);

            await engine.StopAsync();
        }

        [TestMethod]
        public async System.Threading.Tasks.Task LiveSignalMonitor_FlushTimer_FlushesTrailingFramesAutomatically()
        {
            var driver = new MockHardwareService();
            var discovery = await driver.DiscoverInterfacesAsync();
            var iface = discovery.Value![0];
            var options = CanGatewayOptions.CreateClassic(iface.Channels[0], iface.Channels[1], 500000);
            var sessionResult = await driver.OpenGatewaySessionAsync(options);
            var mockSession = (MockCanGatewaySession)sessionResult.Value!;
            await using ICanGatewaySession session = mockSession;

            DbcDocument doc = CreateSampleDocument();
            var plan = new SimulationPlan(doc, [
                new SimulationMessageRule(256, false, true, GatewayMode.PassThrough, SimulationSendType.Cyclic,
                    new SimulationTiming(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), 0), [], new E2eProtectionConfiguration(false))
            ]);
            await using var engine = new SimulationEngine(session, plan);
            await engine.StartAsync();

            using var viewModel = new SimulationViewModel(plan, engine);

            byte[] payload = new byte[] { 0xE8, 0x03, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var frame = CanFrame.CreateClassic(256, false, payload);
            await mockSession.EnqueueReceivedAsync(CanGatewaySide.Tx, frame);

            SignalModel speedSig = viewModel.Signals.First(s => s.Name == "EngineSpeed");
            for (int i = 0; i < 20 && !speedSig.HasReceivedData; i++)
            {
                await System.Threading.Tasks.Task.Delay(50);
            }

            Assert.IsTrue(speedSig.HasReceivedData, "Timer must automatically flush trailing frame to signals");
            Assert.AreEqual("● Active", speedSig.StatusText);
            Assert.AreEqual(125.0, speedSig.Value, 0.001);

            await engine.StopAsync();
        }

        [TestMethod]
        public async System.Threading.Tasks.Task LiveSignalMonitor_WhenSignalIsOverridden_DisplaysInjectedValueAndRedStatus()
        {
            var driver = new MockHardwareService();
            var discovery = await driver.DiscoverInterfacesAsync();
            var iface = discovery.Value![0];
            var options = CanGatewayOptions.CreateClassic(iface.Channels[0], iface.Channels[1], 500000);
            var sessionResult = await driver.OpenGatewaySessionAsync(options);
            var mockSession = (MockCanGatewaySession)sessionResult.Value!;
            await using ICanGatewaySession session = mockSession;

            DbcDocument doc = CreateSampleDocument();
            var plan = new SimulationPlan(doc, [
                new SimulationMessageRule(256, false, true, GatewayMode.PassThrough, SimulationSendType.Cyclic,
                    new SimulationTiming(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), 0), [], new E2eProtectionConfiguration(false))
            ]);
            await using var engine = new SimulationEngine(session, plan);
            await engine.StartAsync();

            using var viewModel = new SimulationViewModel(plan, engine);

            // 1. Initial live frame from RX (raw 1000 = 125 rpm)
            byte[] rawPayload = new byte[] { 0xE8, 0x03, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00 };
            await mockSession.EnqueueReceivedAsync(CanGatewaySide.Rx, CanFrame.CreateClassic(256, false, rawPayload));
            await System.Threading.Tasks.Task.Delay(50);
            viewModel.FlushLiveBufferToSignals();

            SignalModel speedSig = viewModel.Signals.First(s => s.Name == "EngineSpeed");
            Assert.IsTrue(speedSig.HasReceivedData);
            Assert.AreEqual("● Active", speedSig.StatusText);
            Assert.AreEqual("#10B981", speedSig.StatusColor);
            Assert.AreEqual("125 rpm", speedSig.PhysicalValueDisplay);
            Assert.AreEqual("0x3E8", speedSig.RawValue);

            // 2. User overrides signal in UI6: Value = 500.0 rpm (raw = 4000 = 0xFA0)
            speedSig.IsOverridden = true;
            speedSig.Value = 500.0;

            Assert.AreEqual("● Injected", speedSig.StatusText, "Status must change to Injected immediately on override");
            Assert.AreEqual("#EF4444", speedSig.StatusColor, "Color must be red #EF4444");
            Assert.AreEqual("500 rpm", speedSig.PhysicalValueDisplay, "Display value must reflect overridden value 500 rpm");
            Assert.AreEqual("0xFA0", speedSig.RawValue, "Raw value must match calculated raw 0xFA0");

            // 3. New incoming raw frame from ECU (still sending 125 rpm and temp 60 degC) must NOT overwrite overridden display
            await mockSession.EnqueueReceivedAsync(CanGatewaySide.Rx, CanFrame.CreateClassic(256, false, rawPayload));
            await System.Threading.Tasks.Task.Delay(50);
            viewModel.FlushLiveBufferToSignals();

            Assert.AreEqual("● Injected", speedSig.StatusText, "Status must remain Injected after subsequent frame");
            Assert.AreEqual("#EF4444", speedSig.StatusColor);
            Assert.AreEqual("500 rpm", speedSig.PhysicalValueDisplay, "Display must still show 500 rpm, not 125 rpm");
            Assert.AreEqual("0xFA0", speedSig.RawValue);

            // Verify non-overridden signal (EngineTemp) remains completely stable and Active
            SignalModel tempSig = viewModel.Signals.First(s => s.Name == "EngineTemp");
            Assert.AreEqual("● Active", tempSig.StatusText, "Non-overridden signal must remain Active");
            Assert.AreEqual("#10B981", tempSig.StatusColor, "Non-overridden signal must remain green #10B981");
            Assert.AreEqual("60 degC", tempSig.PhysicalValueDisplay, "Non-overridden signal must remain 60 degC");
            Assert.AreEqual(60.0, tempSig.Value, 0.001);

            // 4. Untick override: next frame restores normal live display
            speedSig.IsOverridden = false;
            await mockSession.EnqueueReceivedAsync(CanGatewaySide.Rx, CanFrame.CreateClassic(256, false, rawPayload));
            await System.Threading.Tasks.Task.Delay(50);
            viewModel.FlushLiveBufferToSignals();

            Assert.AreEqual("● Active", speedSig.StatusText, "Status must return to Active when override unticked");
            Assert.AreEqual("#10B981", speedSig.StatusColor);
            Assert.AreEqual("125 rpm", speedSig.PhysicalValueDisplay);
            Assert.AreEqual("0x3E8", speedSig.RawValue);
            Assert.AreEqual("60 degC", tempSig.PhysicalValueDisplay);

            await engine.StopAsync();
        }

        [TestMethod]
        public async System.Threading.Tasks.Task SimulationEngine_WhenInjectingFrame_TransmitsModifiedPayloadToDestination()
        {
            var driver = new MockHardwareService();
            var discovery = await driver.DiscoverInterfacesAsync();
            var iface = discovery.Value![0];
            var options = CanGatewayOptions.CreateClassic(iface.Channels[0], iface.Channels[1], 500000);
            var sessionResult = await driver.OpenGatewaySessionAsync(options);
            var mockSession = (MockCanGatewaySession)sessionResult.Value!;
            await using ICanGatewaySession session = mockSession;

            DbcDocument doc = CreateSampleDocument();
            var plan = new SimulationPlan(doc, [
                new SimulationMessageRule(256, false, true, GatewayMode.Inject, SimulationSendType.Cyclic,
                    new SimulationTiming(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), 0),
                    [new SignalOverride("EngineSpeed", 500.0)],
                    new E2eProtectionConfiguration(false))
            ]);
            await using var engine = new SimulationEngine(session, plan);

            await engine.StartAsync();

            // Send raw frame with 125 rpm (0x3E8) from RX
            byte[] rawPayload = new byte[] { 0xE8, 0x03, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00 };
            await mockSession.EnqueueReceivedAsync(CanGatewaySide.Rx, CanFrame.CreateClassic(256, false, rawPayload));

            // Receive transmitted outbound frame
            RoutedCanFrame transmitted = await ReadNextAsync(mockSession.ReceiveTransmittedAsync());
            Assert.AreEqual(CanGatewaySide.Tx, transmitted.Source, "Outbound injected frame destination must be Tx");

            // Verify payload was modified: EngineSpeed (bits 0-15) = 500 / 0.125 = 4000 = 0x0FA0 -> bytes [A0, 0F]
            byte[] injectedBytes = transmitted.Frame.Data.ToArray();
            Assert.AreEqual(0xA0, injectedBytes[0]);
            Assert.AreEqual(0x0F, injectedBytes[1]);
            // Verify non-overridden byte (EngineTemp = 60 degC -> raw 100 = 0x64) is completely preserved!
            Assert.AreEqual(0x64, injectedBytes[2], "Non-overridden signal byte must be preserved intact");

            await engine.StopAsync();
        }

        private static async System.Threading.Tasks.Task<T> ReadNextAsync<T>(System.Collections.Generic.IAsyncEnumerable<T> source)
        {
            using var timeout = new System.Threading.CancellationTokenSource(System.TimeSpan.FromSeconds(2));
            await using var enumerator = source.GetAsyncEnumerator(timeout.Token);
            if (await enumerator.MoveNextAsync())
            {
                return enumerator.Current;
            }
            throw new AssertFailedException("The asynchronous sequence completed before yielding an item.");
        }
    }
}
