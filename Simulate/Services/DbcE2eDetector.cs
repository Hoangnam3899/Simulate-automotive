using System;
using System.Linq;
using Simulate.Models;

namespace Simulate.Services
{
    /// <summary>
    /// Cung cấp giải thuật tự động nhận diện cấu hình E2E (CRC8 + Alive Counter)
    /// từ thông tin tín hiệu trong DBC Message, kèm cấu hình chuẩn dự phòng.
    /// </summary>
    public static class DbcE2eDetector
    {
        /// <summary>
        /// Tự động phát hiện cấu hình E2E từ danh sách tín hiệu trong một DBC message.
        /// </summary>
        /// <param name="message">DBC Message cần kiểm tra.</param>
        /// <returns>Bộ giá trị gồm cờ IsAutoDetected và cấu hình E2eProtectionConfiguration.</returns>
        public static (bool IsAutoDetected, E2eProtectionConfiguration Configuration) Detect(DbcMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);

            if (message.PayloadLength <= 1)
            {
                return (false, new E2eProtectionConfiguration(isEnabled: false));
            }

            DbcSignal? crcSignal = message.Signals.FirstOrDefault(s =>
                s.Name.Contains("CRC", StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains("CheckSum", StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains("Checksum", StringComparison.OrdinalIgnoreCase) ||
                s.Name.EndsWith("_CS", StringComparison.OrdinalIgnoreCase) ||
                s.Name.StartsWith("CS_", StringComparison.OrdinalIgnoreCase));

            DbcSignal? counterSignal = message.Signals.FirstOrDefault(s =>
                s.Name.Contains("Alive", StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains("RollingCounter", StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains("SeqCounter", StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains("CycleCounter", StringComparison.OrdinalIgnoreCase) ||
                (s.Name.Contains("Counter", StringComparison.OrdinalIgnoreCase) && !s.Name.Contains("Fault", StringComparison.OrdinalIgnoreCase)) ||
                s.Name.EndsWith("_SQC", StringComparison.OrdinalIgnoreCase) ||
                s.Name.EndsWith("_AC", StringComparison.OrdinalIgnoreCase));

            if (crcSignal is not null && counterSignal is not null)
            {
                try
                {
                    int checksumByte = (int)(crcSignal.StartBit / 8);
                    int counterByte = (int)(counterSignal.StartBit / 8);

                    if (checksumByte < message.PayloadLength && counterByte < message.PayloadLength && checksumByte != counterByte)
                    {
                        int counterMax = counterSignal.Maximum > 0 ? (int)counterSignal.Maximum : 14;
                        if (counterMax > 15) counterMax = 14;

                        int bitLength = counterSignal.BitLength;
                        if (bitLength <= 0 || bitLength > 8) bitLength = 4;

                        byte counterMask;
                        if (counterSignal.ByteOrder == DbcByteOrder.BigEndian)
                        {
                            int lsbInByte = (int)(counterSignal.StartBit % 8) - bitLength + 1;
                            if (lsbInByte < 0) lsbInByte = 0;
                            counterMask = (byte)(((1 << bitLength) - 1) << lsbInByte);
                        }
                        else
                        {
                            int lsbInByte = (int)(counterSignal.StartBit % 8);
                            counterMask = (byte)(((1 << bitLength) - 1) << lsbInByte);
                        }

                        if (counterMask == 0) counterMask = 0x0F;

                        int crcStart = (checksumByte == 0) ? 1 : 0;
                        int crcEnd = (checksumByte == message.PayloadLength - 1)
                            ? message.PayloadLength - 2
                            : message.PayloadLength - 1;

                        if (crcStart <= crcEnd)
                        {
                            var config = new E2eProtectionConfiguration(
                                isEnabled: true,
                                checksumByteIndex: checksumByte,
                                counterByteIndex: counterByte,
                                counterMask: counterMask,
                                counterMaximumValue: counterMax,
                                crcStartByteIndex: crcStart,
                                crcEndByteIndex: crcEnd);

                            return (true, config);
                        }
                    }
                }
                catch
                {
                    // Fall back if custom parsing fails validation
                }
            }

            return (false, CreateStandardFallback(message.PayloadLength, isEnabled: false));
        }

        /// <summary>
        /// Tạo cấu hình E2E dự phòng chuẩn AUTOSAR Profile 1 (Byte 0 CRC, Byte 1 Counter).
        /// </summary>
        /// <param name="payloadLength">Độ dài payload của message (bytes).</param>
        /// <param name="isEnabled">Trạng thái kích hoạt.</param>
        /// <returns>Đối tượng E2eProtectionConfiguration.</returns>
        public static E2eProtectionConfiguration CreateStandardFallback(int payloadLength, bool isEnabled = true)
        {
            int safeLength = Math.Max(2, payloadLength);
            return new E2eProtectionConfiguration(
                isEnabled: isEnabled,
                checksumByteIndex: 0,
                counterByteIndex: 1,
                counterMask: 0x0F,
                counterMaximumValue: 14,
                crcStartByteIndex: 1,
                crcEndByteIndex: safeLength - 1);
        }
    }
}
