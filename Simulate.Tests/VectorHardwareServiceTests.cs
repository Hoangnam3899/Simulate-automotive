using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class VectorHardwareServiceTests
    {
        [TestMethod]
        public async Task Discovery_failure_returns_native_status_and_closes_the_driver()
        {
            var api = new FakeVectorXlApi
            {
                DriverConfigStatus = new VectorNativeStatus(129, "XL_ERR_HW_NOT_PRESENT")
            };
            var service = new VectorHardwareService(() => api);

            HardwareOperationResult<IReadOnlyList<HardwareInterface>> result =
                await service.DiscoverInterfacesAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Failure);
            Assert.AreEqual(HardwareOperation.DiscoverInterfaces, result.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.DiscoveryFailed, result.Failure.Code);
            Assert.AreEqual(129, result.Failure.NativeStatus);
            StringAssert.Contains(result.Failure.Message, "XL_GetDriverConfig");
            StringAssert.Contains(result.Failure.Message, "XL_ERR_HW_NOT_PRESENT");
            Assert.IsFalse(api.IsDriverOpen);
        }

        [TestMethod]
        public async Task Discovery_returns_only_present_channels_with_an_active_CAN_transceiver()
        {
            var api = new FakeVectorXlApi
            {
                Channels = new VectorChannelDescriptor[]
                {
                    CreateChannel(channelIndex: 1, channelMask: 2, hasActiveCanCapability: true),
                    CreateChannel(channelIndex: 2, channelMask: 4, hasActiveCanCapability: false),
                    CreateChannel(channelIndex: 3, channelMask: 0, hasActiveCanCapability: true),
                    CreateChannel(channelIndex: 4, channelMask: 16, hasActiveCanCapability: true, isPresent: false)
                }
            };
            var service = new VectorHardwareService(() => api);

            HardwareOperationResult<IReadOnlyList<HardwareInterface>> result =
                await service.DiscoverInterfacesAsync();

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(1, result.Value.Count);
            Assert.AreEqual("Virtual CAN Bus 1", result.Value[0].Name);
            Assert.AreEqual(1, result.Value[0].Channels.Count);
            Assert.AreEqual(1, result.Value[0].Channels[0].ChannelIndex);
            Assert.AreEqual(2UL, result.Value[0].Channels[0].ChannelMask);
            Assert.AreEqual(250_000u, result.Value[0].Channels[0].DefaultBaudrate);
            Assert.IsFalse(api.IsDriverOpen);
        }

        [TestMethod]
        public async Task Discovery_reports_the_close_driver_status_instead_of_silently_succeeding()
        {
            var api = new FakeVectorXlApi
            {
                CloseDriverStatus = new VectorNativeStatus(210, "XL_ERR_CONNECTION_BROKEN")
            };
            var service = new VectorHardwareService(() => api);

            HardwareOperationResult<IReadOnlyList<HardwareInterface>> result =
                await service.DiscoverInterfacesAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Failure);
            Assert.AreEqual(HardwareOperation.DiscoverInterfaces, result.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.DiscoveryFailed, result.Failure.Code);
            Assert.AreEqual(210, result.Failure.NativeStatus);
            StringAssert.Contains(result.Failure.Message, "XL_CloseDriver");
            StringAssert.Contains(result.Failure.Message, "XL_ERR_CONNECTION_BROKEN");
        }

        [TestMethod]
        public async Task Configuration_failure_returns_native_status_and_releases_port_and_driver()
        {
            var api = new FakeVectorXlApi
            {
                ConfigurationStatus = new VectorNativeStatus(112, "XL_ERR_INVALID_ACCESS")
            };
            var service = new VectorHardwareService(() => api);

            HardwareOperationResult<ICanGatewaySession> result =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Failure);
            Assert.AreEqual(HardwareOperation.ConfigureSession, result.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.ConfigurationFailed, result.Failure.Code);
            Assert.AreEqual(112, result.Failure.NativeStatus);
            StringAssert.Contains(result.Failure.Message, "XL_CanSetChannelBitrate");
            StringAssert.Contains(result.Failure.Message, "XL_ERR_INVALID_ACCESS");
            Assert.AreEqual(VectorCanInterfaceVersion.Version3, api.OpenedInterfaceVersion);
            Assert.IsFalse(api.AreChannelsActive);
            Assert.IsFalse(api.IsPortOpen);
            Assert.IsFalse(api.IsDriverOpen);
        }

        [TestMethod]
        public async Task CAN_FD_configuration_failure_uses_interface_V4_and_releases_native_resources()
        {
            var api = new FakeVectorXlApi
            {
                ConfigurationStatus = new VectorNativeStatus(515, "XL_ERR_INVALID_FDFLAG_MODE20")
            };
            var service = new VectorHardwareService(() => api);

            HardwareOperationResult<ICanGatewaySession> result =
                await service.OpenGatewaySessionAsync(CreateFlexibleDataRateOptions());

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Failure);
            Assert.AreEqual(HardwareOperation.ConfigureSession, result.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.ConfigurationFailed, result.Failure.Code);
            Assert.AreEqual(515, result.Failure.NativeStatus);
            StringAssert.Contains(result.Failure.Message, "XL_CanFdSetConfiguration");
            StringAssert.Contains(result.Failure.Message, "XL_ERR_INVALID_FDFLAG_MODE20");
            Assert.AreEqual(VectorCanInterfaceVersion.Version4, api.OpenedInterfaceVersion);
            Assert.IsFalse(api.IsPortOpen);
            Assert.IsFalse(api.IsDriverOpen);
        }

        [TestMethod]
        public async Task Port_open_failure_closes_a_returned_port_handle_and_the_driver()
        {
            var api = new FakeVectorXlApi
            {
                OpenPortStatus = new VectorNativeStatus(204, "XL_ERR_INVALID_CHANNEL_MASK"),
                ReturnPortHandleOnOpenFailure = true
            };
            var service = new VectorHardwareService(() => api);

            HardwareOperationResult<ICanGatewaySession> result =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Failure);
            Assert.AreEqual(HardwareOperation.OpenSession, result.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.OpenFailed, result.Failure.Code);
            Assert.AreEqual(204, result.Failure.NativeStatus);
            StringAssert.Contains(result.Failure.Message, "XL_OpenPort");
            StringAssert.Contains(result.Failure.Message, "XL_ERR_INVALID_CHANNEL_MASK");
            Assert.IsFalse(api.IsPortOpen);
            Assert.IsFalse(api.IsDriverOpen);
        }

        [TestMethod]
        public async Task Activation_failure_returns_native_status_and_releases_port_and_driver()
        {
            var api = new FakeVectorXlApi
            {
                ActivationStatus = new VectorNativeStatus(120, "XL_ERR_HW_NOT_READY")
            };
            var service = new VectorHardwareService(() => api);

            HardwareOperationResult<ICanGatewaySession> result =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Failure);
            Assert.AreEqual(HardwareOperation.ActivateSession, result.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.ActivationFailed, result.Failure.Code);
            Assert.AreEqual(120, result.Failure.NativeStatus);
            StringAssert.Contains(result.Failure.Message, "XL_ActivateChannel");
            StringAssert.Contains(result.Failure.Message, "XL_ERR_HW_NOT_READY");
            Assert.IsFalse(api.AreChannelsActive);
            Assert.IsFalse(api.IsPortOpen);
            Assert.IsFalse(api.IsDriverOpen);
        }

        [TestMethod]
        public void Legacy_connect_activation_failure_does_not_leak_port_or_driver()
        {
            var api = new FakeVectorXlApi
            {
                ActivationStatus = new VectorNativeStatus(120, "XL_ERR_HW_NOT_READY")
            };
            var service = new VectorHardwareService(() => api);
            CanGatewayOptions options = CreateClassicOptions();

            bool connected = service.Connect(
                options.TxChannel,
                options.RxChannel,
                options.NominalBitrate,
                isCanFd: false);

            Assert.IsFalse(connected);
            Assert.IsFalse(service.IsConnected);
            Assert.IsFalse(api.AreChannelsActive);
            Assert.IsFalse(api.IsPortOpen);
            Assert.IsFalse(api.IsDriverOpen);
        }

        [TestMethod]
        public async Task Missing_init_access_fails_before_activation_and_releases_native_resources()
        {
            var api = new FakeVectorXlApi { GrantedPermissionMask = 1 };
            var service = new VectorHardwareService(() => api);

            HardwareOperationResult<ICanGatewaySession> result =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Failure);
            Assert.AreEqual(HardwareOperation.ConfigureSession, result.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.ConfigurationFailed, result.Failure.Code);
            Assert.IsNull(result.Failure.NativeStatus);
            StringAssert.Contains(result.Failure.Message, "init access");
            StringAssert.Contains(result.Failure.Message, "0x3");
            Assert.IsFalse(api.AreChannelsActive);
            Assert.IsFalse(api.IsPortOpen);
            Assert.IsFalse(api.IsDriverOpen);
        }

        [TestMethod]
        public async Task Successful_session_stop_is_idempotent_and_releases_all_native_resources()
        {
            var api = new FakeVectorXlApi();
            var service = new VectorHardwareService(() => api);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());

            Assert.IsTrue(openResult.IsSuccess);
            Assert.IsNotNull(openResult.Value);
            ICanGatewaySession session = openResult.Value;
            Assert.IsTrue(session.IsOpen);
            Assert.IsTrue(api.AreChannelsActive);
            Assert.IsTrue(api.IsPortOpen);
            Assert.IsTrue(api.IsDriverOpen);

            HardwareOperationResult firstStop = await session.StopAsync();
            HardwareOperationResult secondStop = await session.StopAsync();
            await session.DisposeAsync();

            Assert.IsTrue(firstStop.IsSuccess);
            Assert.IsTrue(secondStop.IsSuccess);
            Assert.IsFalse(session.IsOpen);
            Assert.IsFalse(api.AreChannelsActive);
            Assert.IsFalse(api.IsPortOpen);
            Assert.IsFalse(api.IsDriverOpen);
        }

        [TestMethod]
        public async Task Cleanup_reports_deactivation_status_and_still_closes_port_and_driver()
        {
            var api = new FakeVectorXlApi
            {
                DeactivationStatus = new VectorNativeStatus(113, "XL_ERR_PORT_IS_OFFLINE")
            };
            var service = new VectorHardwareService(() => api);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());
            Assert.IsTrue(openResult.IsSuccess);
            ICanGatewaySession session = openResult.Value!;

            HardwareOperationResult stopResult = await session.StopAsync();
            HardwareOperationResult repeatedStopResult = await session.StopAsync();

            Assert.IsFalse(stopResult.IsSuccess);
            Assert.IsNotNull(stopResult.Failure);
            Assert.AreEqual(HardwareOperation.Stop, stopResult.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.StopFailed, stopResult.Failure.Code);
            Assert.AreEqual(113, stopResult.Failure.NativeStatus);
            StringAssert.Contains(stopResult.Failure.Message, "XL_DeactivateChannel");
            StringAssert.Contains(stopResult.Failure.Message, "XL_ERR_PORT_IS_OFFLINE");
            Assert.IsFalse(repeatedStopResult.IsSuccess);
            Assert.IsFalse(api.AreChannelsActive);
            Assert.IsFalse(api.IsPortOpen);
            Assert.IsFalse(api.IsDriverOpen);
        }

        private static CanGatewayOptions CreateClassicOptions()
        {
            var rxChannel = new HardwareChannel
            {
                Name = "RX",
                ChannelIndex = 0,
                ChannelMask = 1
            };
            var txChannel = new HardwareChannel
            {
                Name = "TX",
                ChannelIndex = 1,
                ChannelMask = 2
            };
            return CanGatewayOptions.CreateClassic(rxChannel, txChannel, 500_000);
        }

        private static CanGatewayOptions CreateFlexibleDataRateOptions()
        {
            var rxChannel = new HardwareChannel
            {
                Name = "RX",
                ChannelIndex = 0,
                ChannelMask = 1
            };
            var txChannel = new HardwareChannel
            {
                Name = "TX",
                ChannelIndex = 1,
                ChannelMask = 2
            };
            return CanGatewayOptions.CreateFlexibleDataRate(
                rxChannel,
                txChannel,
                nominalBitrate: 500_000,
                dataBitrate: 2_000_000);
        }

        private static VectorChannelDescriptor CreateChannel(
            int channelIndex,
            ulong channelMask,
            bool hasActiveCanCapability,
            bool isPresent = true)
        {
            return new VectorChannelDescriptor
            {
                HardwareTypeCode = 1,
                HardwareTypeName = "VIRTUAL",
                IsPresent = isPresent,
                IsVirtual = true,
                HardwareIndex = 0,
                HardwareChannel = channelIndex,
                ChannelIndex = channelIndex,
                ChannelMask = channelMask,
                HasActiveCanCapability = hasActiveCanCapability,
                TransceiverName = "Virtual CAN",
                CurrentCanBitrate = 250_000
            };
        }

        private sealed class FakeVectorXlApi : IVectorXlApi
        {
            private const int OpenPortHandle = 42;

            public VectorNativeStatus DriverConfigStatus { get; init; } =
                new VectorNativeStatus(0, "XL_SUCCESS");

            public VectorNativeStatus ConfigurationStatus { get; init; } =
                new VectorNativeStatus(0, "XL_SUCCESS");

            public VectorNativeStatus OpenPortStatus { get; init; } =
                new VectorNativeStatus(0, "XL_SUCCESS");

            public VectorNativeStatus CloseDriverStatus { get; init; } =
                new VectorNativeStatus(0, "XL_SUCCESS");

            public VectorNativeStatus ActivationStatus { get; init; } =
                new VectorNativeStatus(0, "XL_SUCCESS");

            public VectorNativeStatus DeactivationStatus { get; init; } =
                new VectorNativeStatus(0, "XL_SUCCESS");

            public bool IsDriverOpen { get; private set; }

            public bool IsPortOpen { get; private set; }

            public bool AreChannelsActive { get; private set; }

            public ulong? GrantedPermissionMask { get; init; }

            public bool ReturnPortHandleOnOpenFailure { get; init; }

            public VectorCanInterfaceVersion? OpenedInterfaceVersion { get; private set; }

            public IReadOnlyList<VectorChannelDescriptor> Channels { get; init; } =
                Array.Empty<VectorChannelDescriptor>();

            public VectorNativeStatus OpenDriver()
            {
                IsDriverOpen = true;
                return new VectorNativeStatus(0, "XL_SUCCESS");
            }

            public VectorNativeStatus CloseDriver()
            {
                if (CloseDriverStatus.IsSuccess)
                {
                    IsDriverOpen = false;
                }

                return CloseDriverStatus;
            }

            public VectorDriverConfigResult GetDriverConfig()
            {
                return new VectorDriverConfigResult(
                    DriverConfigStatus,
                    Channels);
            }

            public VectorPortOpenResult OpenCanPort(
                ulong accessMask,
                uint receiveQueueSize,
                VectorCanInterfaceVersion interfaceVersion)
            {
                OpenedInterfaceVersion = interfaceVersion;
                bool returnsPortHandle = OpenPortStatus.IsSuccess || ReturnPortHandleOnOpenFailure;
                IsPortOpen = returnsPortHandle;
                return new VectorPortOpenResult(
                    OpenPortStatus,
                    returnsPortHandle ? OpenPortHandle : VectorNativeSessionResources.InvalidPortHandle,
                    GrantedPermissionMask ?? accessMask);
            }

            public VectorNativeStatus SetClassicCanBitrate(
                int portHandle,
                ulong accessMask,
                uint bitrate)
            {
                return ConfigurationStatus;
            }

            public VectorNativeStatus SetCanFdBitrates(
                int portHandle,
                ulong accessMask,
                uint nominalBitrate,
                uint dataBitrate)
            {
                return ConfigurationStatus;
            }

            public VectorNativeStatus ActivateCanChannels(int portHandle, ulong accessMask)
            {
                if (ActivationStatus.IsSuccess)
                {
                    AreChannelsActive = true;
                }

                return ActivationStatus;
            }

            public VectorNativeStatus DeactivateChannels(int portHandle, ulong accessMask)
            {
                if (DeactivationStatus.IsSuccess)
                {
                    AreChannelsActive = false;
                }

                return DeactivationStatus;
            }

            public VectorNativeStatus ClosePort(int portHandle)
            {
                IsPortOpen = false;
                AreChannelsActive = false;
                return new VectorNativeStatus(0, "XL_SUCCESS");
            }
        }
    }
}
