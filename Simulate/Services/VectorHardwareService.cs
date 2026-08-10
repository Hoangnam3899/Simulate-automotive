using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simulate.Models;

namespace Simulate.Services
{
    public class VectorHardwareService : ICanConnectionDriver, ICanHardwareDriver
    {
        private readonly Func<IVectorXlApi> _apiFactory;
        private ICanGatewaySession? _legacySession;

        public bool IsConnected { get; private set; }

        public VectorHardwareService()
            : this(() => new VectorXlApi())
        {
        }

        internal VectorHardwareService(Func<IVectorXlApi> apiFactory)
        {
            _apiFactory = apiFactory ?? throw new ArgumentNullException(nameof(apiFactory));
        }

        public List<HardwareInterface> GetAvailableInterfaces()
        {
            HardwareOperationResult<IReadOnlyList<HardwareInterface>> result =
                DiscoverInterfacesAsync().GetAwaiter().GetResult();

            return result.IsSuccess
                ? new List<HardwareInterface>(result.Value!)
                : new List<HardwareInterface>();
        }

        public Task<HardwareOperationResult<IReadOnlyList<HardwareInterface>>> DiscoverInterfacesAsync(
            CancellationToken cancellationToken = default)
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
            return Task.FromResult(result);
        }

        public Task<HardwareOperationResult<ICanGatewaySession>> OpenGatewaySessionAsync(
            CanGatewayOptions options,
            CancellationToken cancellationToken = default)
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
                        VectorNativeStatus configurationStatus = options.BusMode == CanBusMode.Classic
                            ? api.SetClassicCanBitrate(
                                openPortResult.PortHandle,
                                accessMask,
                                options.NominalBitrate)
                            : api.SetCanFdBitrates(
                                openPortResult.PortHandle,
                                accessMask,
                                options.NominalBitrate,
                                options.DataBitrate!.Value);

                        if (!configurationStatus.IsSuccess)
                        {
                            string configurationApi = options.BusMode == CanBusMode.Classic
                                ? "XL_CanSetChannelBitrate"
                                : "XL_CanFdSetConfiguration";
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

                return Task.FromResult(
                    HardwareOperationResult.Failed<ICanGatewaySession>(failure));
            }

            ICanGatewaySession session = new VectorCanGatewaySession(options, resources!);
            return Task.FromResult(HardwareOperationResult.Succeeded(session));
        }

        public bool Connect(HardwareChannel txChannel, HardwareChannel rxChannel, uint baudrate, bool isCanFd)
        {
            if (IsConnected)
            {
                return true;
            }

            try
            {
                CanGatewayOptions options = isCanFd
                    ? CanGatewayOptions.CreateFlexibleDataRate(
                        rxChannel,
                        txChannel,
                        baudrate,
                        checked(baudrate * 4))
                    : CanGatewayOptions.CreateClassic(rxChannel, txChannel, baudrate);
                HardwareOperationResult<ICanGatewaySession> result =
                    OpenGatewaySessionAsync(options).GetAwaiter().GetResult();
                if (!result.IsSuccess)
                {
                    return false;
                }

                _legacySession = result.Value;
                IsConnected = true;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        public bool Disconnect()
        {
            ICanGatewaySession? session = _legacySession;
            _legacySession = null;
            IsConnected = false;

            if (session is null)
            {
                return true;
            }

            try
            {
                HardwareOperationResult stopResult =
                    session.StopAsync().AsTask().GetAwaiter().GetResult();
                session.DisposeAsync().AsTask().GetAwaiter().GetResult();
                return stopResult.IsSuccess;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static List<HardwareInterface> MapCanInterfaces(
            IReadOnlyList<VectorChannelDescriptor> channels)
        {
            var interfacesByDevice = new Dictionary<string, HardwareInterface>();

            foreach (VectorChannelDescriptor channel in channels)
            {
                if (!IsValidCanChannel(channel))
                {
                    continue;
                }

                string deviceKey = $"{channel.HardwareTypeCode}_{channel.HardwareIndex}";
                if (!interfacesByDevice.TryGetValue(deviceKey, out HardwareInterface? hardwareInterface))
                {
                    string deviceName = channel.IsVirtual
                        ? $"Virtual CAN Bus {channel.HardwareIndex + 1}"
                        : $"{channel.HardwareTypeName} {channel.HardwareIndex + 1}";
                    hardwareInterface = new HardwareInterface { Name = deviceName };
                    interfacesByDevice.Add(deviceKey, hardwareInterface);
                }

                string channelName = string.IsNullOrWhiteSpace(channel.TransceiverName)
                    ? $"Channel {channel.HardwareChannel + 1}"
                    : $"{channel.TransceiverName} (CH {channel.HardwareChannel + 1})";
                uint bitrate = channel.CurrentCanBitrate == 0 ? 500_000u : channel.CurrentCanBitrate;

                hardwareInterface.Channels.Add(new HardwareChannel
                {
                    Name = channelName,
                    ChannelIndex = channel.ChannelIndex,
                    ChannelMask = channel.ChannelMask,
                    DefaultBaudrate = bitrate
                });
            }

            return new List<HardwareInterface>(interfacesByDevice.Values);
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
