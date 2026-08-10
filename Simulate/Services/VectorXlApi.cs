using System;
using System.Collections.Generic;
using vxlapi_NET;

namespace Simulate.Services
{
    internal readonly record struct VectorNativeStatus(int Code, string Name)
    {
        public bool IsSuccess => Code == (int)XLDefine.XL_Status.XL_SUCCESS;

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
