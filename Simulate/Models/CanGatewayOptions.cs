using System;

namespace Simulate.Models
{
    public sealed class CanGatewayOptions
    {
        private CanGatewayOptions(
            HardwareChannel rxChannel,
            HardwareChannel txChannel,
            CanBusMode busMode,
            uint rxNominalBitrate,
            uint txNominalBitrate,
            uint? rxDataBitrate,
            uint? txDataBitrate)
        {
            ValidateChannels(rxChannel, txChannel);
            ValidateBitrate(nameof(rxNominalBitrate), rxNominalBitrate, "RX nominal");
            ValidateBitrate(nameof(txNominalBitrate), txNominalBitrate, "TX nominal");
            ValidateDataBitrates(
                busMode,
                rxNominalBitrate,
                txNominalBitrate,
                rxDataBitrate,
                txDataBitrate);

            RxChannel = rxChannel;
            TxChannel = txChannel;
            BusMode = busMode;
            RxNominalBitrate = rxNominalBitrate;
            TxNominalBitrate = txNominalBitrate;
            RxDataBitrate = rxDataBitrate;
            TxDataBitrate = txDataBitrate;
        }

        public HardwareChannel RxChannel { get; }

        public HardwareChannel TxChannel { get; }

        public CanBusMode BusMode { get; }

        public uint RxNominalBitrate { get; }

        public uint TxNominalBitrate { get; }

        public uint NominalBitrate => TxNominalBitrate;

        public uint? RxDataBitrate { get; }

        public uint? TxDataBitrate { get; }

        public uint? DataBitrate => TxDataBitrate;

        public static CanGatewayOptions CreateClassic(
            HardwareChannel rxChannel,
            HardwareChannel txChannel,
            uint nominalBitrate)
        {
            return CreateClassic(rxChannel, txChannel, nominalBitrate, nominalBitrate);
        }

        public static CanGatewayOptions CreateClassic(
            HardwareChannel rxChannel,
            HardwareChannel txChannel,
            uint rxNominalBitrate,
            uint txNominalBitrate)
        {
            return new CanGatewayOptions(
                rxChannel,
                txChannel,
                CanBusMode.Classic,
                rxNominalBitrate,
                txNominalBitrate,
                rxDataBitrate: null,
                txDataBitrate: null);
        }

        public static CanGatewayOptions CreateFlexibleDataRate(
            HardwareChannel rxChannel,
            HardwareChannel txChannel,
            uint nominalBitrate,
            uint dataBitrate)
        {
            return CreateFlexibleDataRate(
                rxChannel,
                txChannel,
                nominalBitrate,
                nominalBitrate,
                dataBitrate,
                dataBitrate);
        }

        public static CanGatewayOptions CreateFlexibleDataRate(
            HardwareChannel rxChannel,
            HardwareChannel txChannel,
            uint rxNominalBitrate,
            uint txNominalBitrate,
            uint rxDataBitrate,
            uint txDataBitrate)
        {
            return new CanGatewayOptions(
                rxChannel,
                txChannel,
                CanBusMode.FlexibleDataRate,
                rxNominalBitrate,
                txNominalBitrate,
                rxDataBitrate,
                txDataBitrate);
        }

        private static void ValidateBitrate(string parameterName, uint bitrate, string bitrateName)
        {
            if (bitrate == 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    bitrate,
                    $"The {bitrateName} bitrate must be greater than zero.");
            }
        }

        private static void ValidateDataBitrates(
            CanBusMode busMode,
            uint rxNominalBitrate,
            uint txNominalBitrate,
            uint? rxDataBitrate,
            uint? txDataBitrate)
        {
            if (busMode == CanBusMode.Classic)
            {
                if (rxDataBitrate.HasValue || txDataBitrate.HasValue)
                {
                    throw new ArgumentException("Classic CAN does not use data-phase bitrates.");
                }

                return;
            }

            if (!rxDataBitrate.HasValue || !txDataBitrate.HasValue)
            {
                throw new ArgumentException("CAN FD requires data-phase bitrates for both RX and TX channels.");
            }

            ValidateBitrate(nameof(rxDataBitrate), rxDataBitrate.Value, "RX data-phase");
            ValidateBitrate(nameof(txDataBitrate), txDataBitrate.Value, "TX data-phase");

            if (rxDataBitrate.Value < rxNominalBitrate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rxDataBitrate),
                    rxDataBitrate,
                    "The RX CAN FD data bitrate must be greater than or equal to the RX nominal bitrate.");
            }

            if (txDataBitrate.Value < txNominalBitrate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(txDataBitrate),
                    txDataBitrate,
                    "The TX CAN FD data bitrate must be greater than or equal to the TX nominal bitrate.");
            }
        }

        private static void ValidateChannels(HardwareChannel rxChannel, HardwareChannel txChannel)
        {
            ArgumentNullException.ThrowIfNull(rxChannel);
            ArgumentNullException.ThrowIfNull(txChannel);

            if (rxChannel.ChannelIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rxChannel),
                    rxChannel.ChannelIndex,
                    "The RX channel index cannot be negative.");
            }

            if (txChannel.ChannelIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(txChannel),
                    txChannel.ChannelIndex,
                    "The TX channel index cannot be negative.");
            }

            if (rxChannel.ChannelMask == 0)
            {
                throw new ArgumentException("The RX channel mask cannot be zero.", nameof(rxChannel));
            }

            if (txChannel.ChannelMask == 0)
            {
                throw new ArgumentException("The TX channel mask cannot be zero.", nameof(txChannel));
            }

            if (rxChannel.ChannelIndex == txChannel.ChannelIndex ||
                (rxChannel.ChannelMask & txChannel.ChannelMask) != 0)
            {
                throw new ArgumentException(
                    "RX and TX must identify different hardware channels.",
                    nameof(txChannel));
            }
        }
    }
}
