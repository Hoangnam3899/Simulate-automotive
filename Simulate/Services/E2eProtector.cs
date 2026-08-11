using System;
using Simulate.Models;

namespace Simulate.Services
{
    public static class E2eProtector
    {
        public static E2eProtectionResult Apply(
            Span<byte> payload,
            E2eProtectionConfiguration configuration,
            int previousCounter)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            if (!configuration.IsEnabled)
            {
                return new E2eProtectionResult(isApplied: false, previousCounter, checksum: null);
            }

            ValidateEnabledConfiguration(payload, configuration, previousCounter);

            int counter = previousCounter == configuration.CounterMaximumValue
                ? 0
                : previousCounter + 1;
            byte existingCounterByte = payload[configuration.CounterByteIndex];
            byte counterBits = (byte)((counter << configuration.CounterShift) & configuration.CounterMask);
            payload[configuration.CounterByteIndex] = (byte)(
                (existingCounterByte & ~configuration.CounterMask) | counterBits);

            int crcLength = configuration.CrcEndByteIndex - configuration.CrcStartByteIndex + 1;
            byte checksum = Crc8SaeJ1850.Calculate(
                payload.Slice(configuration.CrcStartByteIndex, crcLength));
            payload[configuration.ChecksumByteIndex] = checksum;

            return new E2eProtectionResult(isApplied: true, counter, checksum);
        }

        private static void ValidateEnabledConfiguration(
            ReadOnlySpan<byte> payload,
            E2eProtectionConfiguration configuration,
            int previousCounter)
        {
            if (payload.Length > CanFrame.MaximumPayloadLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(payload),
                    payload.Length,
                    $"A CAN payload cannot exceed {CanFrame.MaximumPayloadLength} bytes.");
            }

            if (configuration.ChecksumByteIndex >= payload.Length
                || configuration.CounterByteIndex >= payload.Length
                || configuration.CrcEndByteIndex >= payload.Length)
            {
                throw new ArgumentException(
                    "The enabled E2E configuration does not fit the payload.",
                    nameof(payload));
            }

            if (configuration.ChecksumByteIndex == configuration.CounterByteIndex)
            {
                throw new ArgumentException(
                    "The checksum byte and counter byte must be different.",
                    nameof(configuration));
            }

            if (configuration.ChecksumByteIndex >= configuration.CrcStartByteIndex
                && configuration.ChecksumByteIndex <= configuration.CrcEndByteIndex)
            {
                throw new ArgumentException(
                    "The checksum byte must be excluded from the configured CRC range.",
                    nameof(configuration));
            }

            if (previousCounter < -1 || previousCounter > configuration.CounterMaximumValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(previousCounter),
                    previousCounter,
                    "The previous counter must be -1 or within the configured counter range.");
            }
        }
    }
}
