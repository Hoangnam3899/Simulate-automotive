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
    }
}
