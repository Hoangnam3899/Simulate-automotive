using System;
using System.Numerics;
using Simulate.Models;

namespace Simulate.Services
{
    public static class SignalCodec
    {
        public static double UnpackPhysical(ReadOnlySpan<byte> payload, DbcSignal signal)
        {
            ArgumentNullException.ThrowIfNull(signal);

            ValidateSignalLayout(payload, signal);

            ulong rawValue = 0;
            int payloadBit = signal.StartBit;
            for (int index = 0; index < signal.BitLength; index++)
            {
                int rawBit = signal.ByteOrder == DbcByteOrder.LittleEndian
                    ? index
                    : signal.BitLength - 1 - index;
                if (ReadBit(payload, payloadBit))
                {
                    rawValue |= 1UL << rawBit;
                }

                payloadBit = GetNextPayloadBit(payloadBit, signal.ByteOrder);
            }

            double numericRawValue = signal.IsSigned
                ? DecodeSignedRawValue(rawValue, signal.BitLength)
                : rawValue;
            double physicalValue = (numericRawValue * signal.Factor) + signal.Offset;
            if (!double.IsFinite(physicalValue))
            {
                throw new InvalidOperationException("The decoded physical value is not finite.");
            }

            return physicalValue;
        }

        private static long DecodeSignedRawValue(ulong rawValue, int bitLength)
        {
            if (bitLength == 64)
            {
                return unchecked((long)rawValue);
            }

            ulong signBit = 1UL << (bitLength - 1);
            return (rawValue & signBit) == 0
                ? (long)rawValue
                : unchecked((long)(rawValue | (ulong.MaxValue << bitLength)));
        }

        public static void PackPhysical(Span<byte> payload, DbcSignal signal, double physicalValue)
        {
            ArgumentNullException.ThrowIfNull(signal);

            ValidateSignalLayout(payload, signal);
            if (!double.IsFinite(physicalValue)
                || physicalValue < signal.Minimum
                || physicalValue > signal.Maximum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(physicalValue),
                    physicalValue,
                    "The physical value must be finite and within the DBC signal range.");
            }

            if (signal.Factor == 0d)
            {
                throw new ArgumentException("The DBC signal factor cannot be zero when packing.", nameof(signal));
            }

            double roundedRawValue = Math.Round(
                (physicalValue - signal.Offset) / signal.Factor,
                MidpointRounding.ToEven);
            if (!double.IsFinite(roundedRawValue))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(physicalValue),
                    physicalValue,
                    "The physical value cannot be represented by the DBC signal.");
            }

            BigInteger rawInteger = new BigInteger(roundedRawValue);
            BigInteger rawModulus = BigInteger.One << signal.BitLength;
            BigInteger minimumRawValue = signal.IsSigned
                ? -(BigInteger.One << (signal.BitLength - 1))
                : BigInteger.Zero;
            BigInteger maximumRawValue = signal.IsSigned
                ? (BigInteger.One << (signal.BitLength - 1)) - BigInteger.One
                : rawModulus - BigInteger.One;
            if (rawInteger < minimumRawValue || rawInteger > maximumRawValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(physicalValue),
                    physicalValue,
                    "The physical value cannot be represented by the DBC signal.");
            }

            BigInteger encodedRawInteger = rawInteger.Sign < 0
                ? rawInteger + rawModulus
                : rawInteger;
            ulong rawValue = (ulong)encodedRawInteger;
            int payloadBit = signal.StartBit;
            for (int index = 0; index < signal.BitLength; index++)
            {
                int rawBit = signal.ByteOrder == DbcByteOrder.LittleEndian
                    ? index
                    : signal.BitLength - 1 - index;
                WriteBit(payload, payloadBit, (rawValue & (1UL << rawBit)) != 0);
                payloadBit = GetNextPayloadBit(payloadBit, signal.ByteOrder);
            }
        }

        private static void ValidateSignalLayout(ReadOnlySpan<byte> payload, DbcSignal signal)
        {
            if (payload.Length > CanFrame.MaximumPayloadLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(payload),
                    payload.Length,
                    $"A CAN payload cannot exceed {CanFrame.MaximumPayloadLength} bytes.");
            }

            int payloadBitLength = payload.Length * 8;
            if (signal.StartBit < 0
                || signal.StartBit >= payloadBitLength
                || signal.BitLength is < 1 or > 64)
            {
                throw new ArgumentException("The DBC signal layout does not fit the payload.", nameof(signal));
            }

            int payloadBit = signal.StartBit;
            for (int index = 0; index < signal.BitLength; index++)
            {
                if (payloadBit < 0 || payloadBit >= payloadBitLength)
                {
                    throw new ArgumentException("The DBC signal layout does not fit the payload.", nameof(signal));
                }

                payloadBit = GetNextPayloadBit(payloadBit, signal.ByteOrder);
            }
        }

        private static int GetNextPayloadBit(int currentBit, DbcByteOrder byteOrder)
        {
            if (byteOrder == DbcByteOrder.LittleEndian)
            {
                return currentBit + 1;
            }

            return currentBit % 8 == 0
                ? currentBit + 15
                : currentBit - 1;
        }

        private static void WriteBit(Span<byte> payload, int payloadBit, bool value)
        {
            int byteIndex = payloadBit / 8;
            byte mask = (byte)(1 << (payloadBit % 8));
            payload[byteIndex] = value
                ? (byte)(payload[byteIndex] | mask)
                : (byte)(payload[byteIndex] & ~mask);
        }

        private static bool ReadBit(ReadOnlySpan<byte> payload, int payloadBit)
        {
            int byteIndex = payloadBit / 8;
            byte mask = (byte)(1 << (payloadBit % 8));
            return (payload[byteIndex] & mask) != 0;
        }
    }
}
