using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class DbcE2eDetectorTests
    {
        [TestMethod]
        public void Detect_ReturnsAutoDetected_WhenMessageHasCrcAndCounterSignals()
        {
            var signals = new List<DbcSignal>
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
                    "VCU_ModeGearSts",
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

            var message = new DbcMessage(
                name: "VCU_01",
                identifier: 0x18F001D0,
                isExtendedIdentifier: true,
                payloadLength: 8,
                transmitter: "VCU",
                signals: signals);

            var (isAutoDetected, config) = DbcE2eDetector.Detect(message);

            Assert.IsTrue(isAutoDetected);
            Assert.IsTrue(config.IsEnabled);
            Assert.AreEqual(0, config.ChecksumByteIndex);
            Assert.AreEqual(1, config.CounterByteIndex);
            Assert.AreEqual((byte)0x0F, config.CounterMask);
            Assert.AreEqual(15, config.CounterMaximumValue); // Signal Maximum is 15
            Assert.AreEqual(1, config.CrcStartByteIndex);
            Assert.AreEqual(7, config.CrcEndByteIndex);
        }

        [TestMethod]
        public void Detect_ReturnsNotDetected_WhenMessageHasNoE2eSignals()
        {
            var signals = new List<DbcSignal>
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

            var message = new DbcMessage(
                name: "Dashboard",
                identifier: 0x100,
                isExtendedIdentifier: false,
                payloadLength: 8,
                transmitter: "IPC",
                signals: signals);

            var (isAutoDetected, config) = DbcE2eDetector.Detect(message);

            Assert.IsFalse(isAutoDetected);
            Assert.IsFalse(config.IsEnabled);
        }

        [TestMethod]
        public void CreateStandardFallback_CreatesValidFallbackConfiguration()
        {
            var config = DbcE2eDetector.CreateStandardFallback(payloadLength: 8, isEnabled: true);

            Assert.IsTrue(config.IsEnabled);
            Assert.AreEqual(0, config.ChecksumByteIndex);
            Assert.AreEqual(1, config.CounterByteIndex);
            Assert.AreEqual((byte)0x0F, config.CounterMask);
            Assert.AreEqual(14, config.CounterMaximumValue);
            Assert.AreEqual(1, config.CrcStartByteIndex);
            Assert.AreEqual(7, config.CrcEndByteIndex);
        }
    }
}
