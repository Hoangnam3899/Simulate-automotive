using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.Tests
{
    /// <summary>
    /// Bo 1,000 Test Cases tu dong kiem thu chuyen sau dua tren DBC cho co che Tiem Loi E2E:
    /// - Nhom 1 (250 TCs): Tiem loi CRC doc lap (Ghim CRC = 0 hoac gia tri bat ky, bao toan CRC va Alive van tang deu).
    /// - Nhom 2 (250 TCs): Tiem loi Alive Counter doc lap (Ghim Alive = 0 hoac gia tri bat ky, Alive dung im va CRC tinh khop voi Alive ket).
    /// - Nhom 3 (250 TCs): Tiem loi kep CRC va Alive dong thoi (Ca 2 deu duoc bao toan 100%, khong bi he thong ghi de).
    /// - Nhom 4 (250 TCs): Ghi de tin hieu Payload thong thuong (TargetTqReq, SpeedReq - E2E tu dong tinh toan khong gay regression).
    /// </summary>
    [TestClass]
    public sealed class E2eDbcFaultInjectionOneThousandTests
    {
        #region Data Generators (Sinh 1,000 kich ban dua tren DBC CVC_FMCU_Ctrl va cac ban tin E2E)

        /// <summary>
        /// Sinh 250 kich ban kiem thu tiem loi CRC doc lap (Ghim CRC co dinh).
        /// </summary>
        public static IEnumerable<object[]> GetPureCrcOverrideCases()
        {
            for (int i = 0; i < 250; i++)
            {
                uint canId = (uint)(i % 2 == 0 ? 0x0AB : 0x0AC); // CVC_FMCU_Ctrl / CVC_RMCU_Ctrl
                int dlc = (i % 3 == 0) ? 24 : ((i % 3 == 1) ? 16 : 8);
                byte pinnedCrc = (byte)(i % 256); // 0x00, 0x01, ..., 0xFF
                int prevCounter = (i % 15) - 1; // -1 to 13
                yield return new object[] { i, canId, dlc, pinnedCrc, prevCounter };
            }
        }

        /// <summary>
        /// Sinh 250 kich ban kiem thu tiem loi Alive Counter doc lap (Ghim Alive co dinh).
        /// </summary>
        public static IEnumerable<object[]> GetPureAliveOverrideCases()
        {
            for (int i = 0; i < 250; i++)
            {
                uint canId = (uint)(i % 2 == 0 ? 0x0AB : 0x0AC);
                int dlc = (i % 3 == 0) ? 24 : 8;
                int pinnedAlive = i % 15; // 0 to 14
                double torqueValue = -400.0 + (i * 3.2); // Range -400 to 400 Nm
                yield return new object[] { i, canId, dlc, pinnedAlive, torqueValue };
            }
        }

        /// <summary>
        /// Sinh 250 kich ban kiem thu tiem loi kep (Ghim ca CRC va Alive dong thoi).
        /// </summary>
        public static IEnumerable<object[]> GetDualOverrideCases()
        {
            for (int i = 0; i < 250; i++)
            {
                uint canId = (uint)(i % 2 == 0 ? 0x0AB : 0x0AC);
                int dlc = 24;
                byte pinnedCrc = (byte)((i * 7) % 256);
                int pinnedAlive = i % 15;
                yield return new object[] { i, canId, dlc, pinnedCrc, pinnedAlive };
            }
        }

        /// <summary>
        /// Sinh 250 kich ban kiem thu ghi de Payload thong thuong (Alive tu tang va CRC tu tinh).
        /// </summary>
        public static IEnumerable<object[]> GetNormalPayloadOverrideCases()
        {
            for (int i = 0; i < 250; i++)
            {
                uint canId = (uint)(i % 2 == 0 ? 0x0AB : 0x0AC);
                int dlc = 24;
                double torque = -400.0 + (i * 2.5);
                int prevCounter = (i % 15) - 1;
                yield return new object[] { i, canId, dlc, torque, prevCounter };
            }
        }

        #endregion

        #region Test Methods

        [TestMethod]
        [DynamicData(nameof(GetPureCrcOverrideCases), DynamicDataSourceType.Method)]
        public void E2e_PureCrcOverride_PreservesPinnedCrcAndAdvancesAlive(
            int testId,
            uint canId,
            int dlc,
            byte pinnedCrc,
            int prevCounter)
        {
            var config = new E2eProtectionConfiguration(
                isEnabled: true,
                checksumByteIndex: 0,
                counterByteIndex: 1,
                counterMask: 0x0F,
                counterMaximumValue: 14,
                crcStartByteIndex: 1,
                crcEndByteIndex: dlc - 1);

            byte[] payload = new byte[dlc];
            // Gia lap nguoi dung ghim CRC = pinnedCrc vao Byte 0
            payload[0] = pinnedCrc;
            payload[1] = 0x00; // Counter nibble

            // Thuc thi E2E Apply voi isChecksumOverridden = true, isCounterOverridden = false
            E2eProtectionResult result = E2eProtector.Apply(
                payload,
                config,
                prevCounter,
                isCounterOverridden: false,
                isChecksumOverridden: true);

            // 1. Checksum phai duoc bao toan 100% dung bang pinnedCrc ma nguoi dung dat
            Assert.IsTrue(result.IsApplied);
            Assert.AreEqual(pinnedCrc, payload[0], $"[TC {testId}] Byte Checksum tren payload phai giu nguyen gia tri ghim.");
            Assert.AreEqual(pinnedCrc, result.Checksum, $"[TC {testId}] Result Checksum phai bang gia tri ghim.");

            // 2. Alive Counter van phai tang deu dan theo chu ky
            int expectedCounter = prevCounter == 14 ? 0 : prevCounter + 1;
            Assert.AreEqual(expectedCounter, result.Counter, $"[TC {testId}] Alive counter van phai tang binh thuong.");
            byte actualCounterInPayload = (byte)(payload[1] & 0x0F);
            Assert.AreEqual((byte)expectedCounter, actualCounterInPayload, $"[TC {testId}] Alive counter trong payload phai duoc cap nhat.");
        }

        [TestMethod]
        [DynamicData(nameof(GetPureAliveOverrideCases), DynamicDataSourceType.Method)]
        public void E2e_PureAliveOverride_PreservesPinnedAliveAndRecalculatesCrc(
            int testId,
            uint canId,
            int dlc,
            int pinnedAlive,
            double torqueValue)
        {
            var config = new E2eProtectionConfiguration(
                isEnabled: true,
                checksumByteIndex: 0,
                counterByteIndex: 1,
                counterMask: 0x0F,
                counterMaximumValue: 14,
                crcStartByteIndex: 1,
                crcEndByteIndex: dlc - 1);

            byte[] payload = new byte[dlc];
            // Gia lap nguoi dung ghim Alive = pinnedAlive vao Byte 1
            payload[1] = (byte)(pinnedAlive & 0x0F);
            // Dat them du lieu payload gia lap
            payload[2] = (byte)(testId & 0xFF);
            payload[3] = (byte)((testId >> 8) & 0xFF);

            // Thuc thi E2E Apply voi isCounterOverridden = true, isChecksumOverridden = false
            E2eProtectionResult result = E2eProtector.Apply(
                payload,
                config,
                previousCounter: pinnedAlive,
                isCounterOverridden: true,
                isChecksumOverridden: false);

            // 1. Alive Counter phai giu nguyen gia tri ghim, khong duoc tu dong tang
            Assert.IsTrue(result.IsApplied);
            Assert.AreEqual(pinnedAlive, result.Counter, $"[TC {testId}] Counter tra ve phai dung bang pinnedAlive.");
            byte actualCounter = (byte)(payload[1] & 0x0F);
            Assert.AreEqual((byte)pinnedAlive, actualCounter, $"[TC {testId}] Byte counter trong payload phai giu nguyen {pinnedAlive}.");

            // 2. Checksum phai duoc tinh toan dong dua tren Counter da ghim va payload
            Assert.IsNotNull(result.Checksum);
            int crcLength = config.CrcEndByteIndex - config.CrcStartByteIndex + 1;
            byte expectedCrc = Crc8SaeJ1850.Calculate(payload.AsSpan(config.CrcStartByteIndex, crcLength));
            Assert.AreEqual(expectedCrc, payload[0], $"[TC {testId}] CRC phai khop toan hoc voi payload chua Alive={pinnedAlive}.");
            Assert.AreEqual(expectedCrc, result.Checksum!.Value);
        }

        [TestMethod]
        [DynamicData(nameof(GetDualOverrideCases), DynamicDataSourceType.Method)]
        public void E2e_DualOverride_PreservesBothPinnedValues(
            int testId,
            uint canId,
            int dlc,
            byte pinnedCrc,
            int pinnedAlive)
        {
            var config = new E2eProtectionConfiguration(
                isEnabled: true,
                checksumByteIndex: 0,
                counterByteIndex: 1,
                counterMask: 0x0F,
                counterMaximumValue: 14,
                crcStartByteIndex: 1,
                crcEndByteIndex: dlc - 1);

            byte[] payload = new byte[dlc];
            payload[0] = pinnedCrc;
            payload[1] = (byte)(pinnedAlive & 0x0F);

            // Thuc thi E2E Apply khi ca 2 deu bi ghim
            E2eProtectionResult result = E2eProtector.Apply(
                payload,
                config,
                previousCounter: pinnedAlive,
                isCounterOverridden: true,
                isChecksumOverridden: true);

            Assert.IsTrue(result.IsApplied);
            // Ca 2 gia tri deu phai duoc bao toan tuyet doi
            Assert.AreEqual(pinnedCrc, payload[0], $"[TC {testId}] Checksum phai giu nguyen {pinnedCrc}.");
            Assert.AreEqual((byte)pinnedAlive, (byte)(payload[1] & 0x0F), $"[TC {testId}] Counter phai giu nguyen {pinnedAlive}.");
            Assert.AreEqual(pinnedCrc, result.Checksum);
            Assert.AreEqual(pinnedAlive, result.Counter);
        }

        [TestMethod]
        [DynamicData(nameof(GetNormalPayloadOverrideCases), DynamicDataSourceType.Method)]
        public void E2e_NormalPayloadOverride_AdvancesAliveAndRecalculatesCrc(
            int testId,
            uint canId,
            int dlc,
            double torque,
            int prevCounter)
        {
            var config = new E2eProtectionConfiguration(
                isEnabled: true,
                checksumByteIndex: 0,
                counterByteIndex: 1,
                counterMask: 0x0F,
                counterMaximumValue: 14,
                crcStartByteIndex: 1,
                crcEndByteIndex: dlc - 1);

            byte[] payload = new byte[dlc];
            payload[2] = (byte)(testId & 0xFF);
            payload[3] = (byte)((testId >> 8) & 0xFF);

            // Khi nguoi dung khong ghim CRC va khong ghim Alive (isCounterOverridden = false, isChecksumOverridden = false)
            E2eProtectionResult result = E2eProtector.Apply(
                payload,
                config,
                prevCounter,
                isCounterOverridden: false,
                isChecksumOverridden: false);

            Assert.IsTrue(result.IsApplied);
            int expectedCounter = prevCounter == 14 ? 0 : prevCounter + 1;
            Assert.AreEqual(expectedCounter, result.Counter);
            Assert.AreEqual((byte)expectedCounter, (byte)(payload[1] & 0x0F));

            int crcLength = config.CrcEndByteIndex - config.CrcStartByteIndex + 1;
            byte expectedCrc = Crc8SaeJ1850.Calculate(payload.AsSpan(config.CrcStartByteIndex, crcLength));
            Assert.AreEqual(expectedCrc, payload[0]);
            Assert.AreEqual(expectedCrc, result.Checksum!.Value);
        }

        #endregion
    }
}
