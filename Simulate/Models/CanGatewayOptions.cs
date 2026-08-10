using System;

namespace Simulate.Models
{
    public sealed class CanGatewayOptions
    {
        private CanGatewayOptions(
            HardwareChannel rxChannel,
            HardwareChannel txChannel,
            CanBusMode busMode,
            uint nominalBitrate,
            uint? dataBitrate)
        {
            ValidateChannels(rxChannel, txChannel);

            if (nominalBitrate == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nominalBitrate),
                    nominalBitrate,
                    "The nominal bitrate must be greater than zero.");
            }

            if (busMode == CanBusMode.FlexibleDataRate && (!dataBitrate.HasValue || dataBitrate.Value == 0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dataBitrate),
                    dataBitrate,
                    "CAN FD requires a data-phase bitrate greater than zero.");
            }

            if (busMode == CanBusMode.Classic && dataBitrate.HasValue)
            {
                throw new ArgumentException(
                    "Classic CAN does not use a data-phase bitrate.",
                    nameof(dataBitrate));
            }

            RxChannel = rxChannel;
            TxChannel = txChannel;
            BusMode = busMode;
            NominalBitrate = nominalBitrate;
            DataBitrate = dataBitrate;
        }

        public HardwareChannel RxChannel { get; }

        public HardwareChannel TxChannel { get; }

        public CanBusMode BusMode { get; }

        public uint NominalBitrate { get; }

        public uint? DataBitrate { get; }

        public static CanGatewayOptions CreateClassic(
            HardwareChannel rxChannel,
            HardwareChannel txChannel,
            uint nominalBitrate)
        {
            return new CanGatewayOptions(
                rxChannel,
                txChannel,
                CanBusMode.Classic,
                nominalBitrate,
                dataBitrate: null);
        }

        public static CanGatewayOptions CreateFlexibleDataRate(
            HardwareChannel rxChannel,
            HardwareChannel txChannel,
            uint nominalBitrate,
            uint dataBitrate)
        {
            return new CanGatewayOptions(
                rxChannel,
                txChannel,
                CanBusMode.FlexibleDataRate,
                nominalBitrate,
                dataBitrate);
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
