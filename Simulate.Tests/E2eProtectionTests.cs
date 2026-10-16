using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class E2eProtectionTests
    {
        [TestMethod]
        public void Calculate_returns_the_crc8_sae_j1850_check_value()
        {
            byte[] data = Encoding.ASCII.GetBytes("123456789");

            byte checksum = Crc8SaeJ1850.Calculate(data);

            Assert.AreEqual((byte)0x4B, checksum);
        }

        [TestMethod]
        public void Apply_does_not_change_payload_or_counter_when_protection_is_disabled()
        {
            var configuration = new E2eProtectionConfiguration(isEnabled: false);
            byte[] payload = [0xA5];

            E2eProtectionResult result = E2eProtector.Apply(payload, configuration, previousCounter: 7);

            CollectionAssert.AreEqual(new byte[] { 0xA5 }, payload);
            Assert.IsFalse(result.IsApplied);
            Assert.AreEqual(7, result.Counter);
            Assert.IsNull(result.Checksum);
        }

        [TestMethod]
        public void Apply_writes_the_next_counter_and_crc_without_changing_unprotected_counter_bits()
        {
            var configuration = new E2eProtectionConfiguration(isEnabled: true);
            byte[] payload = [0xAA, 0xF0, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06];

            E2eProtectionResult result = E2eProtector.Apply(payload, configuration, previousCounter: -1);

            CollectionAssert.AreEqual(
                new byte[] { 0xA2, 0xF0, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 },
                payload);
            Assert.IsTrue(result.IsApplied);
            Assert.AreEqual(0, result.Counter);
            Assert.AreEqual((byte)0xA2, result.Checksum);
        }

        [TestMethod]
        public void Apply_places_the_counter_inside_a_high_nibble_mask()
        {
            var configuration = new E2eProtectionConfiguration(
                isEnabled: true,
                checksumByteIndex: 0,
                counterByteIndex: 1,
                counterMask: 0xF0,
                counterMaximumValue: 14,
                crcStartByteIndex: 1,
                crcEndByteIndex: 1);
            byte[] payload = [0xAA, 0x05];

            E2eProtectionResult result = E2eProtector.Apply(payload, configuration, previousCounter: 0);

            CollectionAssert.AreEqual(new byte[] { 0x9F, 0x15 }, payload);
            Assert.AreEqual(1, result.Counter);
            Assert.AreEqual((byte)0x9F, result.Checksum);
        }

        [TestMethod]
        public void Apply_wraps_the_counter_after_its_configured_maximum()
        {
            var configuration = new E2eProtectionConfiguration(isEnabled: true);
            byte[] payload = [0xAA, 0xFF, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06];

            E2eProtectionResult result = E2eProtector.Apply(payload, configuration, previousCounter: 14);

            CollectionAssert.AreEqual(
                new byte[] { 0xA2, 0xF0, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 },
                payload);
            Assert.AreEqual(0, result.Counter);
        }

        [TestMethod]
        public void Apply_rejects_an_enabled_configuration_that_does_not_fit_before_changing_payload()
        {
            var configuration = new E2eProtectionConfiguration(isEnabled: true);
            byte[] payload = [0xA5];

            Assert.ThrowsException<ArgumentException>(
                () => E2eProtector.Apply(payload, configuration, previousCounter: -1));

            CollectionAssert.AreEqual(new byte[] { 0xA5 }, payload);
        }

        [TestMethod]
        public void Apply_rejects_a_counter_outside_the_configured_range_before_changing_payload()
        {
            var configuration = new E2eProtectionConfiguration(isEnabled: true);
            byte[] payload = [0xAA, 0xF0, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06];

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => E2eProtector.Apply(payload, configuration, previousCounter: 15));

            CollectionAssert.AreEqual(
                new byte[] { 0xAA, 0xF0, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 },
                payload);
        }

        [TestMethod]
        public void Configuration_rejects_a_noncontiguous_counter_mask()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => new E2eProtectionConfiguration(isEnabled: true, counterMask: 0x05));
        }

        [TestMethod]
        public void ApplyPreservingCounter_preserves_existing_counter_and_calculates_checksum()
        {
            var configuration = new E2eProtectionConfiguration(isEnabled: true);
            // Payload ban đầu có Counter = 7 (ở byte 1, nibble thấp: 0xF7)
            byte[] payload = [0x00, 0xF7, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66];

            E2eProtectionResult result = E2eProtector.ApplyPreservingCounter(payload, configuration);

            Assert.IsTrue(result.IsApplied);
            Assert.AreEqual(7, result.Counter, "Alive counter phải được giữ nguyên là 7.");
            Assert.AreEqual((byte)0xF7, payload[1], "Byte counter không bị thay đổi.");
            Assert.IsNotNull(result.Checksum);
            Assert.AreEqual(result.Checksum.Value, payload[0], "Checksum được cập nhật vào byte 0.");
        }
        [TestMethod]
        public void Apply_preserves_overridden_checksum_when_flag_is_true()
        {
            var configuration = new E2eProtectionConfiguration(isEnabled: true);
            // Payload with initial Checksum = 0x00 at byte 0, Counter = 0 at byte 1 (nibble 0x00)
            byte[] payload = [0x00, 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66];

            // When isChecksumOverridden = true, Checksum must NOT be overwritten with calculated CRC
            E2eProtectionResult result = E2eProtector.Apply(
                payload,
                configuration,
                previousCounter: -1,
                isCounterOverridden: false,
                isChecksumOverridden: true);

            Assert.IsTrue(result.IsApplied);
            Assert.AreEqual(0, result.Counter, "Counter phai tang len 0 tu -1.");
            Assert.AreEqual((byte)0x00, payload[0], "Byte Checksum phai duoc bao toan 100% o gia tri 0x00.");
            Assert.AreEqual((byte)0x00, result.Checksum, "Result Checksum phai la gia tri duoc bao toan.");
        }

        [TestMethod]
        public void Apply_preserves_overridden_counter_and_calculates_matching_checksum()
        {
            var configuration = new E2eProtectionConfiguration(isEnabled: true);
            // Payload with pinned Counter = 5 (byte 1 = 0x05)
            byte[] payload = [0xAA, 0x05, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66];

            // When isCounterOverridden = true, Counter must NOT increment to 6, and Checksum must match Counter = 5
            E2eProtectionResult result = E2eProtector.Apply(
                payload,
                configuration,
                previousCounter: 5,
                isCounterOverridden: true,
                isChecksumOverridden: false);

            Assert.IsTrue(result.IsApplied);
            Assert.AreEqual(5, result.Counter, "Counter phai duoc bao toan o gia tri 5.");
            Assert.AreEqual((byte)0x05, payload[1], "Byte Counter phai giu nguyen 0x05.");
            Assert.IsNotNull(result.Checksum);
            Assert.AreEqual(result.Checksum.Value, payload[0], "Checksum phai duoc tinh toan dua tren Counter = 5.");

            // Calculate expected CRC manually to verify mathematical consistency
            int crcLength = configuration.CrcEndByteIndex - configuration.CrcStartByteIndex + 1;
            byte expectedCrc = Crc8SaeJ1850.Calculate(payload.AsSpan(configuration.CrcStartByteIndex, crcLength));
            Assert.AreEqual(expectedCrc, payload[0]);
        }

        [TestMethod]
        public void ApplyPreservingCounter_preserves_overridden_checksum()
        {
            var configuration = new E2eProtectionConfiguration(isEnabled: true);
            byte[] payload = [0x55, 0x07, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66];

            E2eProtectionResult result = E2eProtector.ApplyPreservingCounter(
                payload,
                configuration,
                isChecksumOverridden: true);

            Assert.IsTrue(result.IsApplied);
            Assert.AreEqual(7, result.Counter);
            Assert.AreEqual((byte)0x55, payload[0], "Checksum 0x55 phai duoc bao toan.");
        }

        [TestMethod]
        public void DbcE2eDetector_correctly_identifies_checksum_and_counter_signals()
        {
            var config = new E2eProtectionConfiguration(
                isEnabled: true,
                checksumByteIndex: 0,
                counterByteIndex: 1);

            var crcSig = new DbcSignal("CRC_CVC_FMCU_Ctrl", 7, 8, DbcByteOrder.LittleEndian, false, 1, 0, 0, 255, "", Array.Empty<string>(), Array.Empty<DbcValueDescription>());
            var aliveSig = new DbcSignal("ALIVE_CVC_FMCU_Ctrl", 11, 4, DbcByteOrder.LittleEndian, false, 1, 0, 0, 15, "", Array.Empty<string>(), Array.Empty<DbcValueDescription>());
            var torqueSig = new DbcSignal("CVC_FMCU_TargetTqReq", 23, 16, DbcByteOrder.LittleEndian, false, 0.1, -400, -400, 400, "Nm", Array.Empty<string>(), Array.Empty<DbcValueDescription>());

            Assert.IsTrue(DbcE2eDetector.IsChecksumSignal(crcSig, config));
            Assert.IsFalse(DbcE2eDetector.IsCounterSignal(crcSig, config));

            Assert.IsTrue(DbcE2eDetector.IsCounterSignal(aliveSig, config));
            Assert.IsFalse(DbcE2eDetector.IsChecksumSignal(aliveSig, config));

            Assert.IsFalse(DbcE2eDetector.IsChecksumSignal(torqueSig, config));
            Assert.IsFalse(DbcE2eDetector.IsCounterSignal(torqueSig, config));
        }
    }
}
