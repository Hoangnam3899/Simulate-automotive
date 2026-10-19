using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.Tests
{
    /// <summary>
    /// Bo 5,000 Test Cases doc nhat tuyet doi (Distinct Test Cases) kiem thu toan dien:
    /// - Nhom 1 (1,000 TCs): Ghim DONG THOI ca CRC va Alive (Dual Override / Double Fault).
    /// - Nhom 2 (1,000 TCs): Ghim CRC doc lap (Pure CRC Fault / Data Corruption).
    /// - Nhom 3 (1,000 TCs): Ghim Alive doc lap (Pure Alive Fault / Counter Freeze).
    /// - Nhom 4 (1,000 TCs): Bo ghim & Phuc hoi (Unpin / Recovery Transition Delta = 1).
    /// - Nhom 5 (1,000 TCs): Ngat E2E (Disable) & Bao ve phan cung (Resilience / Auto-Recovery).
    /// </summary>
    [TestClass]
    public sealed class E2eComprehensiveFiveThousandTests
    {
        #region 1. Nhom 1: Sinh 1,000 Kich Ban Ghim Crc Va Alive Dong Thoi (Dual Override)

        public static IEnumerable<object[]> GetDualOverride1000Cases()
        {
            int[] dlcOptions = new int[] { 8, 12, 16, 20, 24, 32, 48, 64 };
            uint[] canIdOptions = new uint[] { 0x0AB, 0x0AC, 0x1A0, 0x2B4, 0x3C8, 0x4D2, 0x5E6, 0x6FA };

            for (int i = 0; i < 1000; i++)
            {
                uint canId = canIdOptions[i % canIdOptions.Length];
                int dlc = dlcOptions[(i / 3) % dlcOptions.Length];
                byte pinnedCrc = (byte)((i * 37 + 13) % 256);
                int pinnedAlive = (i * 7 + 3) % 15; // 0 to 14
                int prevCounter = (i % 16) - 1;     // -1 to 14
                byte highNibbleInCounterByte = (byte)((i * 19) & 0xF0);

                yield return new object[] { i + 1, canId, dlc, pinnedCrc, pinnedAlive, prevCounter, highNibbleInCounterByte };
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetDualOverride1000Cases), DynamicDataSourceType.Method)]
        public void E2E_Group1_DualOverride_PreservesBothCrcAndAliveExactly_1000Cases(
            int testId,
            uint canId,
            int dlc,
            byte pinnedCrc,
            int pinnedAlive,
            int prevCounter,
            byte highNibbleInCounterByte)
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
            // Dat du lieu payload phong phu
            for (int b = 2; b < dlc; b++)
            {
                payload[b] = (byte)((testId * 11 + b * 17) & 0xFF);
            }

            // Dat gia tri ghim vao payload
            payload[0] = pinnedCrc;
            payload[1] = (byte)(highNibbleInCounterByte | (pinnedAlive & 0x0F));

            E2eProtectionResult result = E2eProtector.Apply(
                payload,
                config,
                previousCounter: prevCounter,
                isCounterOverridden: true,
                isChecksumOverridden: true);

            // Assert: Ca Checksum va Alive deu duoc bao toan 100%, khong bi he thong tinh de
            Assert.IsTrue(result.IsApplied, $"[TC {testId}] E2E phai duoc apply.");
            Assert.AreEqual(pinnedCrc, payload[0], $"[TC {testId}] CRC tren payload phai giu nguyen gia tri ghim.");
            Assert.AreEqual(pinnedCrc, result.Checksum, $"[TC {testId}] Result Checksum phai bang gia tri ghim.");

            byte actualCounter = (byte)(payload[1] & 0x0F);
            Assert.AreEqual((byte)pinnedAlive, actualCounter, $"[TC {testId}] Alive counter trong payload phai dung bang pinnedAlive.");
            Assert.AreEqual(pinnedAlive, result.Counter, $"[TC {testId}] Result counter phai dung bang pinnedAlive.");

            // Assert: Bitmask isolation - 4 bit cao cua byte 1 khong bi lam hong
            Assert.AreEqual((byte)highNibbleInCounterByte, (byte)(payload[1] & 0xF0), $"[TC {testId}] 4-bit cao cua byte counter phai duoc bao toan.");
        }

        #endregion

        #region 2. Nhom 2: Sinh 1,000 Kich Ban Ghim Crc Doc Lap (Pure CRC Override)

        public static IEnumerable<object[]> GetPureCrcOverride1000Cases()
        {
            int[] dlcOptions = new int[] { 8, 16, 24, 32, 64 };
            uint[] canIdOptions = new uint[] { 0x0AB, 0x0AC, 0x110, 0x171 };

            for (int i = 0; i < 1000; i++)
            {
                uint canId = canIdOptions[i % canIdOptions.Length];
                int dlc = dlcOptions[(i / 5) % dlcOptions.Length];
                byte pinnedCrc = (byte)((i * 43 + 29) % 256);
                int prevCounter = (i % 15); // 0 to 14
                byte noisePattern = (byte)((i * 31) & 0xFF);

                yield return new object[] { i + 1, canId, dlc, pinnedCrc, prevCounter, noisePattern };
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetPureCrcOverride1000Cases), DynamicDataSourceType.Method)]
        public void E2E_Group2_PureCrcOverride_PreservesCrcAndIncrementsAlive_1000Cases(
            int testId,
            uint canId,
            int dlc,
            byte pinnedCrc,
            int prevCounter,
            byte noisePattern)
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
            payload[1] = 0x50; // high nibble = 0x50
            for (int b = 2; b < dlc; b++)
            {
                payload[b] = (byte)(noisePattern ^ b);
            }

            E2eProtectionResult result = E2eProtector.Apply(
                payload,
                config,
                previousCounter: prevCounter,
                isCounterOverridden: false,
                isChecksumOverridden: true);

            // Assert: CRC giu nguyen gia tri ghim
            Assert.IsTrue(result.IsApplied);
            Assert.AreEqual(pinnedCrc, payload[0], $"[TC {testId}] CRC tren payload phai bang pinnedCrc.");
            Assert.AreEqual(pinnedCrc, result.Checksum, $"[TC {testId}] Result Checksum phai bang pinnedCrc.");

            // Assert: Alive counter phai tang binh thuong
            int expectedCounter = prevCounter == 14 ? 0 : prevCounter + 1;
            Assert.AreEqual(expectedCounter, result.Counter, $"[TC {testId}] Counter phai tang len {expectedCounter}.");
            Assert.AreEqual((byte)expectedCounter, (byte)(payload[1] & 0x0F), $"[TC {testId}] Payload Counter nibble phai dung.");
            Assert.AreEqual((byte)0x50, (byte)(payload[1] & 0xF0), $"[TC {testId}] High nibble cua byte counter phai duoc bao toan.");
        }

        #endregion

        #region 3. Nhom 3: Sinh 1,000 Kich Ban Ghim Alive Doc Lap (Pure Alive Override)

        public static IEnumerable<object[]> GetPureAliveOverride1000Cases()
        {
            int[] dlcOptions = new int[] { 8, 12, 16, 24, 32, 48, 64 };

            for (int i = 0; i < 1000; i++)
            {
                int dlc = dlcOptions[i % dlcOptions.Length];
                int pinnedAlive = (i * 11) % 15; // 0 to 14
                byte highNibble = (byte)((i * 13) & 0xF0);
                uint simulatedTorqueRaw = (uint)(i * 47);

                yield return new object[] { i + 1, dlc, pinnedAlive, highNibble, simulatedTorqueRaw };
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetPureAliveOverride1000Cases), DynamicDataSourceType.Method)]
        public void E2E_Group3_PureAliveOverride_PreservesCounterAndIsolatesBits_1000Cases(
            int testId,
            int dlc,
            int pinnedAlive,
            byte highNibble,
            uint simulatedTorqueRaw)
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
            payload[1] = (byte)(highNibble | (pinnedAlive & 0x0F));
            payload[2] = (byte)(simulatedTorqueRaw & 0xFF);
            payload[3] = (byte)((simulatedTorqueRaw >> 8) & 0xFF);

            E2eProtectionResult result = E2eProtector.Apply(
                payload,
                config,
                previousCounter: pinnedAlive,
                isCounterOverridden: true,
                isChecksumOverridden: false);

            Assert.IsTrue(result.IsApplied);
            Assert.AreEqual(pinnedAlive, result.Counter, $"[TC {testId}] Counter tra ve phai dung bang pinnedAlive.");
            Assert.AreEqual((byte)pinnedAlive, (byte)(payload[1] & 0x0F), $"[TC {testId}] Payload Counter nibble phai giu nguyen {pinnedAlive}.");
            Assert.AreEqual((byte)highNibble, (byte)(payload[1] & 0xF0), $"[TC {testId}] Bitmask isolation: 4-bit cao phai giu nguyen.");
            Assert.IsNotNull(result.Checksum, $"[TC {testId}] Checksum phai duoc tinh.");
        }

        #endregion

        #region 4. Nhom 4: Sinh 1,000 Kich Ban Bo Ghim & Phuc Hoi (Unpin / Recovery Delta = 1)

        public static IEnumerable<object[]> GetUnpinRecovery1000Cases()
        {
            int[] dlcOptions = new int[] { 8, 16, 24, 32, 64 };

            for (int i = 0; i < 1000; i++)
            {
                int dlc = dlcOptions[i % dlcOptions.Length];
                int previouslyPinnedAlive = i % 15; // 0 to 14
                byte lastPinnedCrc = (byte)((i * 23) % 256);
                int cycleSteps = (i % 5) + 1; // 1 to 5 continuous valid recovery cycles

                yield return new object[] { i + 1, dlc, previouslyPinnedAlive, lastPinnedCrc, cycleSteps };
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetUnpinRecovery1000Cases), DynamicDataSourceType.Method)]
        public void E2E_Group4_UnpinRecovery_ResumesCounterWithDeltaOneAndValidCrc_1000Cases(
            int testId,
            int dlc,
            int previouslyPinnedAlive,
            byte lastPinnedCrc,
            int cycleSteps)
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
            int currentCounter = previouslyPinnedAlive;

            // Kiem tra chuoi phuc hoi qua nhieu chu ky (Recovery sequence)
            for (int step = 1; step <= cycleSteps; step++)
            {
                // Ngay khi unpin: isCounterOverridden = false, isChecksumOverridden = false
                E2eProtectionResult result = E2eProtector.Apply(
                    payload,
                    config,
                    previousCounter: currentCounter,
                    isCounterOverridden: false,
                    isChecksumOverridden: false);

                int expectedCounter = (currentCounter == 14) ? 0 : currentCounter + 1;
                Assert.IsTrue(result.IsApplied, $"[TC {testId} Step {step}] E2E phai duoc apply.");
                Assert.AreEqual(expectedCounter, result.Counter, $"[TC {testId} Step {step}] Counter phai tang dung Delta=1 tu {currentCounter} len {expectedCounter}.");
                Assert.AreEqual((byte)expectedCounter, (byte)(payload[1] & 0x0F));

                // Kiem tra CRC tinh toan lai khop 100% voi SAE J1850
                int crcLen = config.CrcEndByteIndex - config.CrcStartByteIndex + 1;
                byte mathCrc = Crc8SaeJ1850.Calculate(payload.AsSpan(config.CrcStartByteIndex, crcLen));
                Assert.AreEqual(mathCrc, payload[0], $"[TC {testId} Step {step}] Checksum phai khop voi toan hoc SAE J1850.");
                Assert.AreEqual(mathCrc, result.Checksum!.Value);

                currentCounter = result.Counter;
            }
        }

        #endregion

        #region 5. Nhom 5: Sinh 1,000 Kich Ban Ngat E2E & Bao Ve Phan Cung (Disable & Resilience)

        public static IEnumerable<object[]> GetDisableAndResilience1000Cases()
        {
            int[] dlcOptions = new int[] { 8, 12, 16, 20, 24, 32, 48, 64 };

            for (int i = 0; i < 1000; i++)
            {
                int dlc = dlcOptions[i % dlcOptions.Length];
                bool isExplicitlyDisabled = (i < 500); // 500 TCs Disable E2E, 500 TCs Hardware Buffer Resilience
                byte originalByte0 = (byte)((i * 17 + 5) & 0xFF);
                byte originalByte1 = (byte)((i * 19 + 7) & 0xFF);
                int simulatedPrevCounter = (i % 15);

                yield return new object[] { i + 1, dlc, isExplicitlyDisabled, originalByte0, originalByte1, simulatedPrevCounter };
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetDisableAndResilience1000Cases), DynamicDataSourceType.Method)]
        public void E2E_Group5_DisableAndResilience_PreservesRawPayloadAndSafeState_1000Cases(
            int testId,
            int dlc,
            bool isExplicitlyDisabled,
            byte originalByte0,
            byte originalByte1,
            int simulatedPrevCounter)
        {
            var config = new E2eProtectionConfiguration(isEnabled: !isExplicitlyDisabled);

            byte[] payload = new byte[dlc];
            payload[0] = originalByte0;
            payload[1] = originalByte1;
            for (int b = 2; b < dlc; b++)
            {
                payload[b] = (byte)((testId + b) & 0xFF);
            }

            if (isExplicitlyDisabled)
            {
                // Kiem thu khao sat ngat E2E (Disable)
                E2eProtectionResult result = E2eProtector.Apply(
                    payload,
                    config,
                    previousCounter: simulatedPrevCounter,
                    isCounterOverridden: false,
                    isChecksumOverridden: false);

                Assert.IsFalse(result.IsApplied, $"[TC {testId}] Khi Disable, IsApplied phai la false.");
                Assert.AreEqual(originalByte0, payload[0], $"[TC {testId}] Byte 0 khong duoc bi thay doi khi E2E tat.");
                Assert.AreEqual(originalByte1, payload[1], $"[TC {testId}] Byte 1 khong duoc bi thay doi khi E2E tat.");
                Assert.IsNull(result.Checksum, $"[TC {testId}] Checksum phai la null.");
            }
            else
            {
                // Kiem thu resilience: Tinh toan voi DLC day du khong nem bat ky exception nao
                var fullConfig = new E2eProtectionConfiguration(
                    isEnabled: true,
                    checksumByteIndex: 0,
                    counterByteIndex: 1,
                    counterMask: 0x0F,
                    counterMaximumValue: 14,
                    crcStartByteIndex: 1,
                    crcEndByteIndex: dlc - 1);

                E2eProtectionResult result = E2eProtector.Apply(
                    payload,
                    fullConfig,
                    previousCounter: simulatedPrevCounter,
                    isCounterOverridden: false,
                    isChecksumOverridden: false);

                Assert.IsTrue(result.IsApplied);
                Assert.IsNotNull(result.Checksum);
                Assert.IsTrue(result.Counter >= 0 && result.Counter <= 14);
            }
        }

        #endregion
    }
}
