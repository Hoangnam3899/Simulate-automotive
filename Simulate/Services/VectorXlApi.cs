using System;
using System.Collections.Generic;
using vxlapi_NET;

namespace Simulate.Services
{
    internal readonly record struct VectorNativeStatus(int Code, string Name)
    {
        public bool IsSuccess => Code == (int)XLDefine.XL_Status.XL_SUCCESS;

        public bool IsQueueEmpty => Code == (int)XLDefine.XL_Status.XL_ERR_QUEUE_IS_EMPTY;

        public static VectorNativeStatus From(XLDefine.XL_Status status)
        {
            return new VectorNativeStatus((int)status, status.ToString());
        }
    }

    internal sealed class VectorChannelDescriptor
    {
        public int HardwareTypeCode { get; init; }

        public string HardwareTypeName { get; init; } = string.Empty;

        public bool IsPresent { get; init; }

        public bool IsVirtual { get; init; }

        public int HardwareIndex { get; init; }

        public int HardwareChannel { get; init; }

        public int ChannelIndex { get; init; }

        public ulong ChannelMask { get; init; }

        public bool HasActiveCanCapability { get; init; }

        public string TransceiverName { get; init; } = string.Empty;

        public uint CurrentCanBitrate { get; init; }
    }

    internal sealed class VectorDriverConfigResult
    {
        public VectorDriverConfigResult(
            VectorNativeStatus status,
            IReadOnlyList<VectorChannelDescriptor> channels)
        {
            Status = status;
            Channels = channels ?? throw new ArgumentNullException(nameof(channels));
        }

        public VectorNativeStatus Status { get; }

        public IReadOnlyList<VectorChannelDescriptor> Channels { get; }
    }

    internal enum VectorCanInterfaceVersion
    {
        Version3 = 3,
        Version4 = 4
    }

    internal readonly record struct VectorPortOpenResult(
        VectorNativeStatus Status,
        int PortHandle,
        ulong PermissionMask);

    [Flags]
    internal enum VectorClassicCanEventFlags
    {
        None = 0,
        ErrorFrame = 1,
        RemoteFrame = 2,
        TransmitCompleted = 4,
        TransmitRequest = 8,
        QueueOverrun = 16
    }

    internal sealed class VectorClassicCanEvent
    {
        public VectorClassicCanEvent(
            int channelIndex,
            uint rawIdentifier,
            ushort dataLength,
            VectorClassicCanEventFlags flags,
            byte[] data,
            ulong timestampNanoseconds)
        {
            ChannelIndex = channelIndex;
            RawIdentifier = rawIdentifier;
            DataLength = dataLength;
            Flags = flags;
            Data = data is null ? throw new ArgumentNullException(nameof(data)) : (byte[])data.Clone();
            TimestampNanoseconds = timestampNanoseconds;
        }

        public int ChannelIndex { get; }

        public uint RawIdentifier { get; }

        public ushort DataLength { get; }

        public VectorClassicCanEventFlags Flags { get; }

        public byte[] Data { get; }

        public ulong TimestampNanoseconds { get; }
    }

    internal readonly record struct VectorClassicReceiveBatchResult(
        VectorNativeStatus Status,
        IReadOnlyList<VectorClassicCanEvent> Events);

    internal readonly record struct VectorClassicFlushResult(
        VectorNativeStatus ReceiveStatus,
        VectorNativeStatus TransmitStatus);

    internal interface IVectorXlApi
    {
        VectorNativeStatus OpenDriver();

        VectorNativeStatus CloseDriver();

        VectorDriverConfigResult GetDriverConfig();

        VectorPortOpenResult OpenCanPort(
            ulong accessMask,
            uint receiveQueueSize,
            VectorCanInterfaceVersion interfaceVersion);

        VectorNativeStatus SetClassicCanBitrate(
            int portHandle,
            ulong accessMask,
            uint bitrate);

        VectorNativeStatus SetCanFdBitrates(
            int portHandle,
            ulong accessMask,
            uint nominalBitrate,
            uint dataBitrate);

        VectorNativeStatus ActivateCanChannels(int portHandle, ulong accessMask);

        VectorNativeStatus TransmitClassicCanFrame(
            int portHandle,
            ulong accessMask,
            uint identifier,
            bool isExtendedIdentifier,
            ReadOnlyMemory<byte> data);

        VectorClassicReceiveBatchResult ReceiveClassicCanEvents(
            int portHandle,
            int maximumEventCount);

        VectorNativeStatus FlushReceiveQueue(int portHandle);

        VectorNativeStatus FlushClassicTransmitQueue(int portHandle, ulong accessMask);

        VectorNativeStatus DeactivateChannels(int portHandle, ulong accessMask);

        VectorNativeStatus ClosePort(int portHandle);
    }

    internal sealed class VectorXlApi : IVectorXlApi
    {
        private readonly XLDriver _driver = new XLDriver();

        public VectorNativeStatus OpenDriver()
        {
            return VectorNativeStatus.From(_driver.XL_OpenDriver());
        }

        public VectorNativeStatus CloseDriver()
        {
            return VectorNativeStatus.From(_driver.XL_CloseDriver());
        }

        public VectorDriverConfigResult GetDriverConfig()
        {
            var config = new XLClass.xl_driver_config();
            VectorNativeStatus status = VectorNativeStatus.From(_driver.XL_GetDriverConfig(ref config));
            if (!status.IsSuccess)
            {
                return new VectorDriverConfigResult(status, Array.Empty<VectorChannelDescriptor>());
            }

            int channelCount = Math.Min((int)config.channelCount, config.channel.Length);
            var channels = new List<VectorChannelDescriptor>(channelCount);
            for (int index = 0; index < channelCount; index++)
            {
                XLClass.xl_channel_config channel = config.channel[index];
                // XL Driver Library Manual 20.30, section 3.3.2 (p. 61): applications
                // searching by bus type must use the corresponding XL_BUS_ACTIVE_CAP flag.
                bool hasActiveCanCapability =
                    (channel.channelBusCapabilities & XLDefine.XL_BusCapabilities.XL_BUS_ACTIVE_CAP_CAN) != 0;
                uint currentCanBitrate = channel.busParams.busType == XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN
                    ? channel.busParams.dataCan.bitrate
                    : 0;

                channels.Add(new VectorChannelDescriptor
                {
                    HardwareTypeCode = (int)channel.hwType,
                    HardwareTypeName = channel.hwType.ToString().Replace("XL_HWTYPE_", string.Empty),
                    IsPresent = channel.hwType != XLDefine.XL_HardwareType.XL_HWTYPE_NONE,
                    IsVirtual = channel.hwType == XLDefine.XL_HardwareType.XL_HWTYPE_VIRTUAL,
                    HardwareIndex = channel.hwIndex,
                    HardwareChannel = channel.hwChannel,
                    ChannelIndex = channel.channelIndex,
                    ChannelMask = channel.channelMask,
                    HasActiveCanCapability = hasActiveCanCapability,
                    TransceiverName = channel.transceiverName ?? string.Empty,
                    CurrentCanBitrate = currentCanBitrate
                });
            }

            return new VectorDriverConfigResult(status, channels);
        }

        public VectorPortOpenResult OpenCanPort(
            ulong accessMask,
            uint receiveQueueSize,
            VectorCanInterfaceVersion interfaceVersion)
        {
            int portHandle = XLDefine.XL_INVALID_PORTHANDLE;
            ulong permissionMask = accessMask;
            VectorNativeStatus status = VectorNativeStatus.From(_driver.XL_OpenPort(
                ref portHandle,
                "Simulate",
                accessMask,
                ref permissionMask,
                receiveQueueSize,
                (XLDefine.XL_InterfaceVersion)interfaceVersion,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN));
            return new VectorPortOpenResult(status, portHandle, permissionMask);
        }

        public VectorNativeStatus SetClassicCanBitrate(
            int portHandle,
            ulong accessMask,
            uint bitrate)
        {
            return VectorNativeStatus.From(
                _driver.XL_CanSetChannelBitrate(portHandle, accessMask, bitrate));
        }

        public VectorNativeStatus SetCanFdBitrates(
            int portHandle,
            ulong accessMask,
            uint nominalBitrate,
            uint dataBitrate)
        {
            var configuration = new XLClass.XLcanFdConf
            {
                arbitrationBitRate = nominalBitrate,
                sjwAbr = 2,
                tseg1Abr = 5,
                tseg2Abr = 2,
                dataBitRate = dataBitrate,
                sjwDbr = 2,
                tseg1Dbr = 5,
                tseg2Dbr = 2,
                options = (byte)XLDefine.XL_CANFD_ConfigOptions.XL_CANFD_CONFOPT_NO_ISO
            };

            return VectorNativeStatus.From(
                _driver.XL_CanFdSetConfiguration(portHandle, accessMask, configuration));
        }

        public VectorNativeStatus ActivateCanChannels(int portHandle, ulong accessMask)
        {
            return VectorNativeStatus.From(_driver.XL_ActivateChannel(
                portHandle,
                accessMask,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN,
                XLDefine.XL_AC_Flags.XL_ACTIVATE_NONE));
        }

        public VectorNativeStatus TransmitClassicCanFrame(
            int portHandle,
            ulong accessMask,
            uint identifier,
            bool isExtendedIdentifier,
            ReadOnlyMemory<byte> data)
        {
            var transmitEvent = new XLClass.xl_event
            {
                tag = XLDefine.XL_EventTags.XL_TRANSMIT_MSG
            };
            transmitEvent.tagData.can_Msg.id = isExtendedIdentifier
                ? identifier | (uint)XLDefine.XL_MessageFlagsExtended.XL_CAN_EXT_MSG_ID
                : identifier;
            transmitEvent.tagData.can_Msg.flags = XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_NONE;
            transmitEvent.tagData.can_Msg.dlc = checked((ushort)data.Length);
            data.Span.CopyTo(transmitEvent.tagData.can_Msg.data);

            // XL Driver Library Manual 20.30, sections 4.3.13 and 4.5.1
            // (pp. 90-94): Classic CAN uses XL_TRANSMIT_MSG with xlCanTransmit.
            return VectorNativeStatus.From(
                _driver.XL_CanTransmit(portHandle, accessMask, transmitEvent));
        }

        public VectorClassicReceiveBatchResult ReceiveClassicCanEvents(
            int portHandle,
            int maximumEventCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEventCount);
            List<VectorClassicCanEvent>? receivedEvents = null;
            VectorNativeStatus status = VectorNativeStatus.From(XLDefine.XL_Status.XL_SUCCESS);

            for (int index = 0; index < maximumEventCount; index++)
            {
                var nativeEvent = new XLClass.xl_event();
                status = VectorNativeStatus.From(_driver.XL_Receive(portHandle, ref nativeEvent));
                if (!status.IsSuccess)
                {
                    break;
                }

                if (nativeEvent.tag != XLDefine.XL_EventTags.XL_RECEIVE_MSG)
                {
                    continue;
                }

                XLClass.xl_can_msg message = nativeEvent.tagData.can_Msg;
                VectorClassicCanEventFlags flags = MapClassicEventFlags(
                    nativeEvent.flags,
                    message.flags);
                receivedEvents ??= new List<VectorClassicCanEvent>(
                    Math.Min(maximumEventCount, 32));
                receivedEvents.Add(new VectorClassicCanEvent(
                    nativeEvent.chanIndex,
                    message.id,
                    message.dlc,
                    flags,
                    message.data,
                    nativeEvent.timeStamp));
            }

            // Manual 20.30, section 3.2.18 (p. 49): drain XL_Receive until
            // XL_ERR_QUEUE_IS_EMPTY before waiting for more driver events.
            IReadOnlyList<VectorClassicCanEvent> events = receivedEvents is null
                ? Array.Empty<VectorClassicCanEvent>()
                : receivedEvents;
            return new VectorClassicReceiveBatchResult(status, events);
        }

        public VectorNativeStatus FlushReceiveQueue(int portHandle)
        {
            // Manual 20.30, section 3.2.15 (p. 47).
            return VectorNativeStatus.From(_driver.XL_FlushReceiveQueue(portHandle));
        }

        public VectorNativeStatus FlushClassicTransmitQueue(int portHandle, ulong accessMask)
        {
            // Manual 20.30, section 4.3.14 (p. 91).
            return VectorNativeStatus.From(
                _driver.XL_CanFlushTransmitQueue(portHandle, accessMask));
        }

        private static VectorClassicCanEventFlags MapClassicEventFlags(
            XLDefine.XL_MessageFlags eventFlags,
            XLDefine.XL_MessageFlags messageFlags)
        {
            VectorClassicCanEventFlags result = VectorClassicCanEventFlags.None;
            if ((eventFlags & XLDefine.XL_MessageFlags.XL_EVENT_FLAG_OVERRUN) != 0 ||
                (messageFlags & XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_OVERRUN) != 0)
            {
                result |= VectorClassicCanEventFlags.QueueOverrun;
            }

            if ((messageFlags & XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_ERROR_FRAME) != 0)
            {
                result |= VectorClassicCanEventFlags.ErrorFrame;
            }

            if ((messageFlags & XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_REMOTE_FRAME) != 0)
            {
                result |= VectorClassicCanEventFlags.RemoteFrame;
            }

            if ((messageFlags & XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_TX_COMPLETED) != 0)
            {
                result |= VectorClassicCanEventFlags.TransmitCompleted;
            }

            if ((messageFlags & XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_TX_REQUEST) != 0)
            {
                result |= VectorClassicCanEventFlags.TransmitRequest;
            }

            return result;
        }

        public VectorNativeStatus DeactivateChannels(int portHandle, ulong accessMask)
        {
            return VectorNativeStatus.From(_driver.XL_DeactivateChannel(portHandle, accessMask));
        }

        public VectorNativeStatus ClosePort(int portHandle)
        {
            return VectorNativeStatus.From(_driver.XL_ClosePort(portHandle));
        }
    }
}
