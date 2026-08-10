using System;

namespace Simulate.Models
{
    public enum CanBusMode
    {
        Classic,
        FlexibleDataRate
    }

    public enum CanGatewaySide
    {
        Rx,
        Tx
    }

    public enum CanFrameFormat
    {
        Classic,
        FlexibleDataRate
    }

    public enum CanDataLengthCode : byte
    {
        Bytes0 = 0,
        Bytes1 = 1,
        Bytes2 = 2,
        Bytes3 = 3,
        Bytes4 = 4,
        Bytes5 = 5,
        Bytes6 = 6,
        Bytes7 = 7,
        Bytes8 = 8,
        Bytes12 = 9,
        Bytes16 = 10,
        Bytes20 = 11,
        Bytes24 = 12,
        Bytes32 = 13,
        Bytes48 = 14,
        Bytes64 = 15
    }

    public sealed class CanFrame
    {
        public const uint MaximumStandardIdentifier = 0x7FF;
        public const uint MaximumExtendedIdentifier = 0x1FFFFFFF;
        public const int MaximumClassicPayloadLength = 8;
        public const int MaximumPayloadLength = 64;

        private readonly byte[] _data;

        private CanFrame(
            uint identifier,
            bool isExtendedIdentifier,
            CanFrameFormat format,
            bool isBitRateSwitchEnabled,
            CanDataLengthCode dataLengthCode,
            ReadOnlySpan<byte> data)
        {
            uint maximumIdentifier = isExtendedIdentifier
                ? MaximumExtendedIdentifier
                : MaximumStandardIdentifier;

            if (identifier > maximumIdentifier)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(identifier),
                    identifier,
                    $"The identifier exceeds the maximum value 0x{maximumIdentifier:X}.");
            }

            if (data.Length > MaximumPayloadLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(data),
                    data.Length,
                    $"A CAN payload cannot exceed {MaximumPayloadLength} bytes.");
            }

            if (format == CanFrameFormat.Classic && isBitRateSwitchEnabled)
            {
                throw new ArgumentException(
                    "Bit rate switching is only valid for CAN FD frames.",
                    nameof(isBitRateSwitchEnabled));
            }

            if (format == CanFrameFormat.Classic && dataLengthCode > CanDataLengthCode.Bytes8)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dataLengthCode),
                    dataLengthCode,
                    "Classic CAN only supports data length codes from 0 to 8.");
            }

            int expectedLength = GetPayloadLength(dataLengthCode);
            if (data.Length != expectedLength)
            {
                throw new ArgumentException(
                    $"The payload length must be {expectedLength} bytes for DLC {(byte)dataLengthCode}.",
                    nameof(data));
            }

            Identifier = identifier;
            IsExtendedIdentifier = isExtendedIdentifier;
            Format = format;
            IsBitRateSwitchEnabled = isBitRateSwitchEnabled;
            DataLengthCode = dataLengthCode;
            _data = data.ToArray();
        }

        public uint Identifier { get; }

        public bool IsExtendedIdentifier { get; }

        public CanFrameFormat Format { get; }

        public bool IsBitRateSwitchEnabled { get; }

        public CanDataLengthCode DataLengthCode { get; }

        public ReadOnlyMemory<byte> Data => _data;

        public int Length => _data.Length;

        public static CanFrame CreateClassic(
            uint identifier,
            bool isExtendedIdentifier,
            ReadOnlySpan<byte> data)
        {
            if (data.Length > MaximumClassicPayloadLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(data),
                    data.Length,
                    $"Classic CAN payloads cannot exceed {MaximumClassicPayloadLength} bytes.");
            }

            return new CanFrame(
                identifier,
                isExtendedIdentifier,
                CanFrameFormat.Classic,
                isBitRateSwitchEnabled: false,
                (CanDataLengthCode)data.Length,
                data);
        }

        public static CanFrame CreateFlexibleDataRate(
            uint identifier,
            bool isExtendedIdentifier,
            CanDataLengthCode dataLengthCode,
            bool isBitRateSwitchEnabled,
            ReadOnlySpan<byte> data)
        {
            return new CanFrame(
                identifier,
                isExtendedIdentifier,
                CanFrameFormat.FlexibleDataRate,
                isBitRateSwitchEnabled,
                dataLengthCode,
                data);
        }

        public static int GetPayloadLength(CanDataLengthCode dataLengthCode)
        {
            return dataLengthCode switch
            {
                CanDataLengthCode.Bytes0 => 0,
                CanDataLengthCode.Bytes1 => 1,
                CanDataLengthCode.Bytes2 => 2,
                CanDataLengthCode.Bytes3 => 3,
                CanDataLengthCode.Bytes4 => 4,
                CanDataLengthCode.Bytes5 => 5,
                CanDataLengthCode.Bytes6 => 6,
                CanDataLengthCode.Bytes7 => 7,
                CanDataLengthCode.Bytes8 => 8,
                CanDataLengthCode.Bytes12 => 12,
                CanDataLengthCode.Bytes16 => 16,
                CanDataLengthCode.Bytes20 => 20,
                CanDataLengthCode.Bytes24 => 24,
                CanDataLengthCode.Bytes32 => 32,
                CanDataLengthCode.Bytes48 => 48,
                CanDataLengthCode.Bytes64 => 64,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(dataLengthCode),
                    dataLengthCode,
                    "The data length code is not valid for CAN.")
            };
        }
    }

    public sealed class RoutedCanFrame
    {
        public RoutedCanFrame(CanGatewaySide source, CanFrame frame, DateTimeOffset timestamp)
        {
            Source = source;
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
            Timestamp = timestamp;
        }

        public CanGatewaySide Source { get; }

        public CanFrame Frame { get; }

        public DateTimeOffset Timestamp { get; }
    }
}
