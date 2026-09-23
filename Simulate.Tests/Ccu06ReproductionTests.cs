using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.Tests
{
    [TestClass]
    public class Ccu06ReproductionTests
    {
        private const string Ccu06Dbc = @"
VERSION """"
NS_ :
BS_:
BU_: CCU VCU BMS GW
BO_ 2566897978 CCU_06: 8 CCU
 SG_ CCU_TMS_OperatingSts : 1|2@0+ (1,0) [0|3] """" VCU,BMS
 SG_ CCU_TMS_Format : 7|4@0+ (1,0) [0|15] """" VCU,BMS
 SG_ CCU_CoolerOutletTemp : 15|8@0+ (1,-40) [-40|215] ""°C"" VCU,BMS
 SG_ CCU_CoolerInletTemp : 23|8@0+ (1,-40) [-40|215] ""°C"" VCU,BMS
 SG_ CCU_CompressorLoad : 31|16@0+ (1,0) [0|10000] ""rpm"" Vector__XXX
 SG_ CCU_TMS_FaultCodes : 55|8@0+ (1,0) [0|255] """" VCU,BMS
 SG_ CCU_TMS_FaultLevel : 63|2@0+ (1,0) [0|3] """" VCU,BMS

VAL_ 2566897978 CCU_TMS_OperatingSts 0 ""OFF"" 1 ""Cooling"" 3 ""Autocyclic"";
VAL_ 2566897978 CCU_TMS_Format 10 ""Integrated"" 13 ""Individual"";
VAL_ 2566897978 CCU_TMS_FaultCodes 0 ""No faullt"";
VAL_ 2566897978 CCU_TMS_FaultLevel 0 ""No faullt"" 1 ""Level 1"" 2 ""Level 2"" 3 ""Level 3"";
";

        [TestMethod]
        public void Test_Ccu06_Motorola_Bit_Packing_And_Unpacking_Isolation()
        {
            var parseResult = DbcParser.Parse(Ccu06Dbc);
            Assert.IsNotNull(parseResult.Document);
            var msg = parseResult.Document.Messages.First();

            var opSts = msg.Signals.First(s => s.Name == "CCU_TMS_OperatingSts");
            var format = msg.Signals.First(s => s.Name == "CCU_TMS_Format");
            var outlet = msg.Signals.First(s => s.Name == "CCU_CoolerOutletTemp");
            var inlet = msg.Signals.First(s => s.Name == "CCU_CoolerInletTemp");
            var comp = msg.Signals.First(s => s.Name == "CCU_CompressorLoad");
            var faultCodes = msg.Signals.First(s => s.Name == "CCU_TMS_FaultCodes");
            var faultLevel = msg.Signals.First(s => s.Name == "CCU_TMS_FaultLevel");

            byte[] payload = new byte[8];

            // 1. Pack base vehicle payload:
            // CCU_TMS_OperatingSts = 0 (OFF)
            // CCU_TMS_Format = 0
            // CCU_CoolerOutletTemp = 27 °C
            // CCU_CoolerInletTemp = 28 °C
            // CCU_CompressorLoad = 0 rpm
            // CCU_TMS_FaultCodes = 0
            // CCU_TMS_FaultLevel = 0
            SignalCodec.PackPhysical(payload, opSts, 0.0);
            SignalCodec.PackPhysical(payload, format, 0.0);
            SignalCodec.PackPhysical(payload, outlet, 27.0);
            SignalCodec.PackPhysical(payload, inlet, 28.0);
            SignalCodec.PackPhysical(payload, comp, 0.0);
            SignalCodec.PackPhysical(payload, faultCodes, 0.0);
            SignalCodec.PackPhysical(payload, faultLevel, 0.0);

            Console.WriteLine($"Base payload: {Convert.ToHexString(payload)}");
            Assert.AreEqual("0043440000000000", Convert.ToHexString(payload));

            // Verify unpack
            Assert.AreEqual(0.0, SignalCodec.UnpackPhysical(payload, opSts));
            Assert.AreEqual(0.0, SignalCodec.UnpackPhysical(payload, format));
            Assert.AreEqual(27.0, SignalCodec.UnpackPhysical(payload, outlet));
            Assert.AreEqual(28.0, SignalCodec.UnpackPhysical(payload, inlet));
            Assert.AreEqual(0.0, SignalCodec.UnpackPhysical(payload, comp));
            Assert.AreEqual(0.0, SignalCodec.UnpackPhysical(payload, faultCodes));
            Assert.AreEqual(0.0, SignalCodec.UnpackPhysical(payload, faultLevel));

            // 2. Now pack CCU_TMS_OperatingSts = 1 (Cooling)
            SignalCodec.PackPhysical(payload, opSts, 1.0);
            Console.WriteLine($"After pack opSts=1: {Convert.ToHexString(payload)}");

            // Verify: CCU_TMS_OperatingSts is 1, and CCU_TMS_Format is STILL 0!
            Assert.AreEqual(1.0, SignalCodec.UnpackPhysical(payload, opSts));
            Assert.AreEqual(0.0, SignalCodec.UnpackPhysical(payload, format), "Format must not be corrupted by opSts");

            // 3. Now pack CCU_TMS_Format = 10 (Integrated)
            SignalCodec.PackPhysical(payload, format, 10.0);
            Console.WriteLine($"After pack format=10: {Convert.ToHexString(payload)}");

            // Verify: both opSts=1 and format=10 coexist in Byte 0!
            Assert.AreEqual(1.0, SignalCodec.UnpackPhysical(payload, opSts), "opSts must not be corrupted by format");
            Assert.AreEqual(10.0, SignalCodec.UnpackPhysical(payload, format));

            // 4. Now pack CCU_TMS_FaultLevel = 2 (Level 2)
            SignalCodec.PackPhysical(payload, faultLevel, 2.0);
            Console.WriteLine($"After pack faultLevel=2: {Convert.ToHexString(payload)}");

            Assert.AreEqual(2.0, SignalCodec.UnpackPhysical(payload, faultLevel));
            Assert.AreEqual(0.0, SignalCodec.UnpackPhysical(payload, faultCodes), "faultCodes must not be corrupted by faultLevel");

            // 5. Now pack CCU_TMS_FaultCodes = 63
            SignalCodec.PackPhysical(payload, faultCodes, 63.0);
            Console.WriteLine($"After pack faultCodes=63: {Convert.ToHexString(payload)}");

            Assert.AreEqual(63.0, SignalCodec.UnpackPhysical(payload, faultCodes));
            Assert.AreEqual(2.0, SignalCodec.UnpackPhysical(payload, faultLevel), "faultLevel must not be corrupted by faultCodes");
        }

        [TestMethod]
        public void Test_Ccu06_SignalModel_Selection_And_Live_Frame_Behavior()
        {
            var parseResult = DbcParser.Parse(Ccu06Dbc);
            var msg = parseResult.Document!.Messages.First();
            var opSts = msg.Signals.First(s => s.Name == "CCU_TMS_OperatingSts");
            var faultLevel = msg.Signals.First(s => s.Name == "CCU_TMS_FaultLevel");

            var opModel = new Simulate.ViewModels.SignalModel
            {
                Name = opSts.Name,
                StartBit = opSts.StartBit,
                Length = opSts.BitLength,
                Factor = opSts.Factor,
                Offset = opSts.Offset,
                Unit = opSts.Unit,
                Min = opSts.Minimum,
                Max = opSts.Maximum,
                Value = opSts.Offset,
                MessageId = "0x18FFC13A",
                MessageName = msg.Name,
                RawIdentifier = msg.Identifier,
                IsExtendedIdentifier = msg.IsExtendedIdentifier,
                IsOverridden = false,
                DbcSource = opSts
            };

            // Initially, no frame received
            Assert.AreEqual(0.0, opModel.Value);
            Assert.AreEqual("[0] OFF", opModel.PhysicalValueInput);
            Assert.IsFalse(opModel.IsOverridden);

            // 1. Simulating live CAN bus frames arriving (vehicle is running with OFF = 0)
            opModel.UpdateValue(0, 0.0, DateTime.Now);
            Assert.AreEqual(0.0, opModel.Value);
            Assert.AreEqual("[0] OFF", opModel.PhysicalValueInput);

            // 2. User selects "[1] Cooling" in UI6 ComboBox (preparing fault injection configuration)
            opModel.PhysicalValueInput = "[1] Cooling";

            Assert.AreEqual("[1] Cooling", opModel.PhysicalValueInput);
            Assert.AreEqual(1.0, opModel.ConfiguredValue);
            Assert.IsFalse(opModel.IsOverridden, "Selecting in UI6 ComboBox must NOT force IsOverridden = true");

            // 3. Now live bus frame arrives from vehicle (vehicle still sending OFF = 0)
            opModel.UpdateValue(0, 0.0, DateTime.Now);

            // PhysicalValueInput in UI6 should STILL be "[1] Cooling" because UI6 is the configuration table!
            Assert.AreEqual("[1] Cooling", opModel.PhysicalValueInput, "UI6 ComboBox must NOT jump back to [0] OFF when bus frame arrives!");
            Assert.AreEqual(1.0, opModel.ConfiguredValue);
            Assert.AreEqual("[0] OFF", opModel.PhysicalValueDisplay, "UI4 monitor reflects live bus traffic");
            Assert.AreEqual("● Active", opModel.StatusText);

            // 4. Now user explicitly checks Override to begin injecting
            opModel.IsOverridden = true;
            Assert.IsTrue(opModel.IsOverridden);
            Assert.AreEqual(1.0, opModel.Value);
            Assert.AreEqual("[1] Cooling", opModel.PhysicalValueDisplay);
            Assert.AreEqual("● Injected", opModel.StatusText);
        }

        [TestMethod]
        public void Test_Ccu06_FaultLevel_And_OperatingSts_Dual_ComboBox_Stability()
        {
            var parseResult = DbcParser.Parse(Ccu06Dbc);
            var msg = parseResult.Document!.Messages.First();
            var opSts = msg.Signals.First(s => s.Name == "CCU_TMS_OperatingSts");
            var faultLevel = msg.Signals.First(s => s.Name == "CCU_TMS_FaultLevel");

            var opModel = new Simulate.ViewModels.SignalModel
            {
                Name = opSts.Name,
                StartBit = opSts.StartBit,
                Length = opSts.BitLength,
                Factor = opSts.Factor,
                Offset = opSts.Offset,
                Unit = opSts.Unit,
                Min = opSts.Minimum,
                Max = opSts.Maximum,
                Value = opSts.Offset,
                MessageId = "0x18FFC13A",
                MessageName = msg.Name,
                RawIdentifier = msg.Identifier,
                IsExtendedIdentifier = msg.IsExtendedIdentifier,
                IsOverridden = false,
                DbcSource = opSts
            };

            var faultModel = new Simulate.ViewModels.SignalModel
            {
                Name = faultLevel.Name,
                StartBit = faultLevel.StartBit,
                Length = faultLevel.BitLength,
                Factor = faultLevel.Factor,
                Offset = faultLevel.Offset,
                Unit = faultLevel.Unit,
                Min = faultLevel.Minimum,
                Max = faultLevel.Maximum,
                Value = faultLevel.Offset,
                MessageId = "0x18FFC13A",
                MessageName = msg.Name,
                RawIdentifier = msg.Identifier,
                IsExtendedIdentifier = msg.IsExtendedIdentifier,
                IsOverridden = false,
                DbcSource = faultLevel
            };

            // 1. Initial state check
            Assert.AreEqual("[0] OFF", opModel.PhysicalValueInput);
            Assert.AreEqual("[0] No faullt", faultModel.PhysicalValueInput);

            // 2. High-speed incoming bus traffic (vehicle frames: raw 0x0)
            for (int i = 0; i < 200; i++)
            {
                opModel.UpdateValue(0, 0.0, DateTime.Now);
                faultModel.UpdateValue(0, 0.0, DateTime.Now);
            }
            Assert.AreEqual("[0] OFF", opModel.PhysicalValueDisplay);
            Assert.AreEqual("[0] No faullt", faultModel.PhysicalValueDisplay);

            // 3. User configures both ComboBoxes in UI6
            opModel.PhysicalValueInput = "[3] Autocyclic";
            faultModel.PhysicalValueInput = "[2] Level 2";

            Assert.AreEqual("[3] Autocyclic", opModel.PhysicalValueInput);
            Assert.AreEqual("[2] Level 2", faultModel.PhysicalValueInput);
            Assert.IsFalse(opModel.IsOverridden, "OperatingSts must remain non-overridden during config");
            Assert.IsFalse(faultModel.IsOverridden, "FaultLevel must remain non-overridden during config");

            // 4. More live vehicle frames arrive at 1ms / 10ms rate over the gateway
            for (int i = 0; i < 100; i++)
            {
                opModel.UpdateValue(0, 0.0, DateTime.Now);
                faultModel.UpdateValue(0, 0.0, DateTime.Now);
            }

            // Verify: BOTH ComboBox selections in UI6 remain solid and do NOT jump!
            Assert.AreEqual("[3] Autocyclic", opModel.PhysicalValueInput, "OperatingSts must NOT jump back to [0] OFF");
            Assert.AreEqual("[2] Level 2", faultModel.PhysicalValueInput, "FaultLevel must NOT jump back to [0] No faullt");

            // Verify: UI4 displays live vehicle status (0 = OFF, 0 = No fault)
            Assert.AreEqual("[0] OFF", opModel.PhysicalValueDisplay);
            Assert.AreEqual("[0] No faullt", faultModel.PhysicalValueDisplay);

            // 5. User checks Override for both to start fault injection
            opModel.IsOverridden = true;
            faultModel.IsOverridden = true;

            Assert.AreEqual(3.0, opModel.Value);
            Assert.AreEqual(2.0, faultModel.Value);
            Assert.AreEqual("[3] Autocyclic", opModel.PhysicalValueDisplay);
            Assert.AreEqual("[2] Level 2", faultModel.PhysicalValueDisplay);
            Assert.AreEqual("● Injected", opModel.StatusText);
            Assert.AreEqual("● Injected", faultModel.StatusText);

            // 6. User unticks Override -> preserves UI6 config, and live monitor returns to vehicle live traffic upon next bus frame
            opModel.IsOverridden = false;
            faultModel.IsOverridden = false;

            Assert.AreEqual("[3] Autocyclic", opModel.PhysicalValueInput, "UI6 config must persist when unticked");
            Assert.AreEqual("[2] Level 2", faultModel.PhysicalValueInput, "UI6 config must persist when unticked");

            // Next frame arrives from vehicle
            opModel.UpdateValue(0, 0.0, DateTime.Now);
            faultModel.UpdateValue(0, 0.0, DateTime.Now);

            Assert.AreEqual("[0] OFF", opModel.PhysicalValueDisplay, "UI4 returns to vehicle live traffic");
            Assert.AreEqual("[0] No faullt", faultModel.PhysicalValueDisplay, "UI4 returns to vehicle live traffic");
        }
    }
}
