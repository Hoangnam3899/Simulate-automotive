using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simulate.Models;

namespace Simulate.Services
{
    public class VectorHardwareService : ICanHardwareDriver
    {
        private readonly Func<IVectorXlApi> _apiFactory;

        public VectorHardwareService()
            : this(() => new VectorXlApi())
        {
        }

        internal VectorHardwareService(Func<IVectorXlApi> apiFactory)
        {
            _apiFactory = apiFactory ?? throw new ArgumentNullException(nameof(apiFactory));
        }

        public Task<HardwareOperationResult<IReadOnlyList<HardwareInterface>>> DiscoverInterfacesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() => DiscoverInterfaces(cancellationToken), cancellationToken);
        }

        private HardwareOperationResult<IReadOnlyList<HardwareInterface>> DiscoverInterfaces(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IVectorXlApi? api = null;
            bool driverIsOpen = false;
            HardwareFailure? failure = null;
            IReadOnlyList<HardwareInterface> interfaces = Array.Empty<HardwareInterface>();
            VectorNativeStatus? closeStatus = null;
            Exception? closeException = null;
            HardwareOperation currentOperation = HardwareOperation.OpenDriver;

            try
            {
                api = _apiFactory();
                VectorNativeStatus openStatus = api.OpenDriver();
                if (!openStatus.IsSuccess)
                {
                    failure = CreateNativeFailure(
                        HardwareOperation.OpenDriver,
                        HardwareErrorCode.DriverUnavailable,
                        "XL_OpenDriver",
                        openStatus);
                }
                else
                {
                    driverIsOpen = true;
                    currentOperation = HardwareOperation.DiscoverInterfaces;
                    VectorDriverConfigResult configResult = api.GetDriverConfig();
                    if (!configResult.Status.IsSuccess)
                    {
                        failure = CreateNativeFailure(
                            HardwareOperation.DiscoverInterfaces,
                            HardwareErrorCode.DiscoveryFailed,
                            "XL_GetDriverConfig",
                            configResult.Status);
                    }
                    else
                    {
                        interfaces = MapCanInterfaces(configResult.Channels);
                    }
                }
            }
            catch (Exception exception)
            {
                HardwareErrorCode errorCode = currentOperation == HardwareOperation.OpenDriver
                    ? HardwareErrorCode.DriverUnavailable
                    : HardwareErrorCode.DiscoveryFailed;
                failure = new HardwareFailure(
                    currentOperation,
                    errorCode,
                    $"Vector discovery failed during {currentOperation}: {exception.Message}");
            }
            finally
            {
                if (driverIsOpen && api is not null)
                {
                    try
                    {
                        closeStatus = api.CloseDriver();
                    }
                    catch (Exception exception)
                    {
                        closeException = exception;
                    }
                }
            }

            if (closeStatus.HasValue && !closeStatus.Value.IsSuccess)
            {
                failure = AddCleanupFailure(
                    failure,
                    "XL_CloseDriver",
                    closeStatus.Value);
            }
            else if (closeException is not null)
            {
                failure = AddCleanupException(failure, "XL_CloseDriver", closeException);
            }

            HardwareOperationResult<IReadOnlyList<HardwareInterface>> result = failure is null
                ? HardwareOperationResult.Succeeded(interfaces)
                : HardwareOperationResult.Failed<IReadOnlyList<HardwareInterface>>(failure);
            return result;
        }

        public Task<HardwareOperationResult<ICanGatewaySession>> OpenGatewaySessionAsync(
            CanGatewayOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            return Task.Run(() => OpenGatewaySession(options, cancellationToken), cancellationToken);
        }

        private HardwareOperationResult<ICanGatewaySession> OpenGatewaySession(
            CanGatewayOptions options,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(options);
            cancellationToken.ThrowIfCancellationRequested();

            IVectorXlApi? api = null;
            VectorNativeSessionResources? resources = null;
            HardwareFailure? failure = null;
            HardwareOperation currentOperation = HardwareOperation.OpenDriver;

            try
            {
                api = _apiFactory();
                resources = new VectorNativeSessionResources(api);

                VectorNativeStatus openDriverStatus = api.OpenDriver();
                if (!openDriverStatus.IsSuccess)
                {
                    failure = CreateNativeFailure(
                        HardwareOperation.OpenDriver,
                        HardwareErrorCode.DriverUnavailable,
                        "XL_OpenDriver",
                        openDriverStatus);
                }
                else
                {
                    resources.OwnDriver();
                    ulong accessMask = options.RxChannel.ChannelMask | options.TxChannel.ChannelMask;
                    currentOperation = HardwareOperation.OpenSession;
                    // XL Driver Library Manual 20.30, section 3.2.9 (pp. 43-44):
                    // Classic CAN uses interface V3; CAN FD uses interface V4.
                    VectorCanInterfaceVersion interfaceVersion = options.BusMode == CanBusMode.Classic
                        ? VectorCanInterfaceVersion.Version3
                        : VectorCanInterfaceVersion.Version4;
                    VectorPortOpenResult openPortResult = api.OpenCanPort(
                        accessMask,
                        receiveQueueSize: 8192,
                        interfaceVersion);

                    if (openPortResult.PortHandle != VectorNativeSessionResources.InvalidPortHandle)
                    {
                        resources.OwnPort(openPortResult.PortHandle, accessMask);
                    }

                    if (!openPortResult.Status.IsSuccess)
                    {
                        failure = CreateNativeFailure(
                            HardwareOperation.OpenSession,
                            HardwareErrorCode.OpenFailed,
                            "XL_OpenPort",
                            openPortResult.Status);
                    }
                    else if (openPortResult.PortHandle == VectorNativeSessionResources.InvalidPortHandle)
                    {
                        failure = new HardwareFailure(
                            HardwareOperation.OpenSession,
                            HardwareErrorCode.OpenFailed,
                            "XL_OpenPort returned XL_SUCCESS but did not return a valid port handle.");
                    }
                    else if ((openPortResult.PermissionMask & accessMask) != accessMask)
                    {
                        failure = new HardwareFailure(
                            HardwareOperation.ConfigureSession,
                            HardwareErrorCode.ConfigurationFailed,
                            $"XL_OpenPort granted init access 0x{openPortResult.PermissionMask:X}, " +
                            $"but configuration requires 0x{accessMask:X}.");
                    }
                    else
                    {
                        currentOperation = HardwareOperation.ConfigureSession;
                        VectorNativeStatus configurationStatus;
                        string configurationApi;
                        if (options.BusMode == CanBusMode.Classic)
                        {
                            configurationApi = "XL_CanSetChannelBitrate";
                            if (options.RxNominalBitrate == options.TxNominalBitrate)
                            {
                                configurationStatus = api.SetClassicCanBitrate(
                                    openPortResult.PortHandle,
                                    accessMask,
                                    options.TxNominalBitrate);
                            }
                            else
                            {
                                configurationStatus = api.SetClassicCanBitrate(
                                    openPortResult.PortHandle,
                                    options.RxChannel.ChannelMask,
                                    options.RxNominalBitrate);
                                if (configurationStatus.IsSuccess)
                                {
                                    configurationStatus = api.SetClassicCanBitrate(
                                        openPortResult.PortHandle,
                                        options.TxChannel.ChannelMask,
                                        options.TxNominalBitrate);
                                }
                            }
                        }
                        else
                        {
                            configurationApi = "XL_CanFdSetConfiguration";
                            if (options.RxNominalBitrate == options.TxNominalBitrate &&
                                options.RxDataBitrate == options.TxDataBitrate)
                            {
                                configurationStatus = api.SetCanFdBitrates(
                                    openPortResult.PortHandle,
                                    accessMask,
                                    options.TxNominalBitrate,
                                    options.TxDataBitrate!.Value,
                                    VectorCanFdProtocolMode.Iso);
                            }
                            else
                            {
                                configurationStatus = api.SetCanFdBitrates(
                                    openPortResult.PortHandle,
                                    options.RxChannel.ChannelMask,
                                    options.RxNominalBitrate,
                                    options.RxDataBitrate!.Value,
                                    VectorCanFdProtocolMode.Iso);
                                if (configurationStatus.IsSuccess)
                                {
                                    configurationStatus = api.SetCanFdBitrates(
                                        openPortResult.PortHandle,
                                        options.TxChannel.ChannelMask,
                                        options.TxNominalBitrate,
                                        options.TxDataBitrate!.Value,
                                        VectorCanFdProtocolMode.Iso);
                                }
                            }
                        }

                        if (!configurationStatus.IsSuccess)
                        {
                            failure = CreateNativeFailure(
                                HardwareOperation.ConfigureSession,
                                HardwareErrorCode.ConfigurationFailed,
                                configurationApi,
                                configurationStatus);
                        }
                        else
                        {
                            currentOperation = HardwareOperation.ActivateSession;
                            VectorNativeStatus activationStatus = api.ActivateCanChannels(
                                openPortResult.PortHandle,
                                accessMask);
                            if (!activationStatus.IsSuccess)
                            {
                                failure = CreateNativeFailure(
                                    HardwareOperation.ActivateSession,
                                    HardwareErrorCode.ActivationFailed,
                                    "XL_ActivateChannel",
                                    activationStatus);
                            }
                            else
                            {
                                resources.MarkChannelsActive();
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                failure = new HardwareFailure(
                    currentOperation,
                    GetErrorCode(currentOperation),
                    $"Vector session setup failed during {currentOperation}: {exception.Message}");
            }

            if (failure is not null)
            {
                if (resources is not null)
                {
                    HardwareOperationResult cleanupResult = resources.Cleanup();
                    if (!cleanupResult.IsSuccess)
                    {
                        failure = AddSessionCleanupFailure(failure, cleanupResult.Failure!);
                    }
                }

                return HardwareOperationResult.Failed<ICanGatewaySession>(failure);
            }

            ICanGatewaySession session = new VectorCanGatewaySession(options, resources!);
            return HardwareOperationResult.Succeeded(session);
        }

        private static List<HardwareInterface> MapCanInterfaces(
            IReadOnlyList<VectorChannelDescriptor> channels)
        {
            var validChannels = channels.Where(IsValidCanChannel).ToList();
            if (validChannels.Count == 0)
            {
                return new List<HardwareInterface>();
            }

            var result = new List<HardwareInterface>();

            // 1. Group all Virtual Channels into a single "Virtual CAN" interface
            var virtualChannels = validChannels.Where(c => c.IsVirtual).ToList();
            if (virtualChannels.Count > 0)
            {
                var virtualInterface = new HardwareInterface { Name = "Virtual CAN" };
                bool hasMultipleBuses = virtualChannels.Select(c => c.HardwareIndex).Distinct().Count() > 1;

                foreach (VectorChannelDescriptor channel in virtualChannels)
                {
                    string channelName = hasMultipleBuses
                        ? $"Virtual Bus {channel.HardwareIndex + 1} - Channel {channel.HardwareChannel + 1}"
                        : $"VIRTUAL Channel {channel.HardwareChannel + 1}";
                    uint bitrate = channel.CurrentCanBitrate == 0 ? 500_000u : channel.CurrentCanBitrate;

                    virtualInterface.Channels.Add(new HardwareChannel
                    {
                        Name = channelName,
                        ChannelIndex = channel.ChannelIndex,
                        ChannelMask = channel.ChannelMask,
                        DefaultBaudrate = bitrate
                    });
                }

                result.Add(virtualInterface);
            }

            // 2. Group Physical Hardware Channels by Device
            var physicalChannels = validChannels.Where(c => !c.IsVirtual).ToList();
            var physicalInterfacesByDevice = new Dictionary<string, HardwareInterface>();

            foreach (VectorChannelDescriptor channel in physicalChannels)
            {
                string deviceKey = $"{channel.HardwareTypeCode}_{channel.HardwareIndex}";
                if (!physicalInterfacesByDevice.TryGetValue(deviceKey, out HardwareInterface? hwInterface))
                {
                    string deviceName = $"{channel.HardwareTypeName} {channel.HardwareIndex + 1}";
                    hwInterface = new HardwareInterface { Name = deviceName };
                    physicalInterfacesByDevice.Add(deviceKey, hwInterface);
                    result.Add(hwInterface);
                }

                string prefix = string.IsNullOrWhiteSpace(channel.HardwareTypeName) ? "CAN" : channel.HardwareTypeName;
                string channelName = $"{prefix} Channel {channel.HardwareChannel + 1}";
                uint bitrate = channel.CurrentCanBitrate == 0 ? 500_000u : channel.CurrentCanBitrate;

                hwInterface.Channels.Add(new HardwareChannel
                {
                    Name = channelName,
                    ChannelIndex = channel.ChannelIndex,
                    ChannelMask = channel.ChannelMask,
                    DefaultBaudrate = bitrate
                });
            }

            // 3. If multiple interfaces exist (e.g. Virtual + HW or multiple HW), provide "All Vector Devices"
            if (result.Count > 1)
            {
                var allInterface = new HardwareInterface { Name = "All Vector Devices" };
                foreach (var iface in result)
                {
                    foreach (var ch in iface.Channels)
                    {
                        allInterface.Channels.Add(new HardwareChannel
                        {
                            Name = $"[{iface.Name}] {ch.Name}",
                            ChannelIndex = ch.ChannelIndex,
                            ChannelMask = ch.ChannelMask,
                            DefaultBaudrate = ch.DefaultBaudrate
                        });
                    }
                }

                result.Add(allInterface);
            }

            return result;
        }

        private static bool IsValidCanChannel(VectorChannelDescriptor channel)
        {
            if (!channel.IsPresent || !channel.HasActiveCanCapability ||
                channel.ChannelIndex < 0 || channel.ChannelIndex >= 64)
            {
                return false;
            }

            ulong expectedChannelMask = 1UL << channel.ChannelIndex;
            return channel.ChannelMask == expectedChannelMask;
        }

        private static HardwareFailure CreateNativeFailure(
            HardwareOperation operation,
            HardwareErrorCode errorCode,
            string apiName,
            VectorNativeStatus status)
        {
            return new HardwareFailure(
                operation,
                errorCode,
                $"{apiName} failed with XL_Status {status.Name} ({status.Code}).",
                status.Code);
        }

        private static HardwareErrorCode GetErrorCode(HardwareOperation operation)
        {
            return operation switch
            {
                HardwareOperation.OpenDriver => HardwareErrorCode.DriverUnavailable,
                HardwareOperation.OpenSession => HardwareErrorCode.OpenFailed,
                HardwareOperation.ConfigureSession => HardwareErrorCode.ConfigurationFailed,
                HardwareOperation.ActivateSession => HardwareErrorCode.ActivationFailed,
                _ => HardwareErrorCode.Unexpected
            };
        }

        private static HardwareFailure AddSessionCleanupFailure(
            HardwareFailure setupFailure,
            HardwareFailure cleanupFailure)
        {
            return new HardwareFailure(
                setupFailure.Operation,
                setupFailure.Code,
                $"{setupFailure.Message} Cleanup also failed: {cleanupFailure.Message}",
                setupFailure.NativeStatus ?? cleanupFailure.NativeStatus);
        }

        private static HardwareFailure AddCleanupFailure(
            HardwareFailure? primaryFailure,
            string apiName,
            VectorNativeStatus status)
        {
            if (primaryFailure is null)
            {
                return CreateNativeFailure(
                    HardwareOperation.DiscoverInterfaces,
                    HardwareErrorCode.DiscoveryFailed,
                    apiName,
                    status);
            }

            return new HardwareFailure(
                primaryFailure.Operation,
                primaryFailure.Code,
                $"{primaryFailure.Message} Cleanup also failed: {apiName} returned " +
                $"XL_Status {status.Name} ({status.Code}).",
                primaryFailure.NativeStatus);
        }

        private static HardwareFailure AddCleanupException(
            HardwareFailure? primaryFailure,
            string apiName,
            Exception exception)
        {
            if (primaryFailure is null)
            {
                return new HardwareFailure(
                    HardwareOperation.DiscoverInterfaces,
                    HardwareErrorCode.DiscoveryFailed,
                    $"{apiName} threw while closing the Vector driver: {exception.Message}");
            }

            return new HardwareFailure(
                primaryFailure.Operation,
                primaryFailure.Code,
                $"{primaryFailure.Message} Cleanup also failed: {apiName} threw {exception.Message}.",
                primaryFailure.NativeStatus);
        }
    }
}
