using System;
using System.Numerics;

namespace Simulate.Models
{
    public sealed class E2eProtectionConfiguration
    {
        public E2eProtectionConfiguration(
            bool isEnabled,
            int checksumByteIndex = 0,
            int counterByteIndex = 1,
            byte counterMask = 0x0F,
            int counterMaximumValue = 14,
            int crcStartByteIndex = 1,
            int crcEndByteIndex = 7)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(checksumByteIndex);
            ArgumentOutOfRangeException.ThrowIfNegative(counterByteIndex);
            ArgumentOutOfRangeException.ThrowIfNegative(crcStartByteIndex);

            if (crcEndByteIndex < crcStartByteIndex)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(crcEndByteIndex),
                    crcEndByteIndex,
                    "The CRC end byte must not precede the start byte.");
            }

            int counterShift = BitOperations.TrailingZeroCount((uint)counterMask);
            uint normalizedMask = (uint)counterMask >> counterShift;
            if (counterMask == 0 || (normalizedMask & (normalizedMask + 1)) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(counterMask),
                    counterMask,
                    "The counter mask must contain one contiguous bit field.");
            }

            int counterCapacity = (1 << BitOperations.PopCount((uint)counterMask)) - 1;
            if (counterMaximumValue < 0 || counterMaximumValue > counterCapacity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(counterMaximumValue),
                    counterMaximumValue,
                    "The counter maximum must fit the configured counter mask.");
            }

            IsEnabled = isEnabled;
            ChecksumByteIndex = checksumByteIndex;
            CounterByteIndex = counterByteIndex;
            CounterMask = counterMask;
            CounterMaximumValue = counterMaximumValue;
            CrcStartByteIndex = crcStartByteIndex;
            CrcEndByteIndex = crcEndByteIndex;
            CounterShift = counterShift;
        }

        public bool IsEnabled { get; }

        public int ChecksumByteIndex { get; }

        public int CounterByteIndex { get; }

        public byte CounterMask { get; }

        public int CounterMaximumValue { get; }

        public int CrcStartByteIndex { get; }

        public int CrcEndByteIndex { get; }

        internal int CounterShift { get; }
    }

    public sealed class E2eProtectionResult
    {
        internal E2eProtectionResult(bool isApplied, int counter, byte? checksum)
        {
            IsApplied = isApplied;
            Counter = counter;
            Checksum = checksum;
        }

        public bool IsApplied { get; }

        public int Counter { get; }

        public byte? Checksum { get; }
    }
}
