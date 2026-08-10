using System;
using System.Collections.Generic;
using System.Threading;
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
        public async Task Classic_session_transmits_a_classic_frame()
        {
            var api = new FakeVectorXlApi();
            var service = new VectorHardwareService(() => api);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());
            Assert.IsTrue(openResult.IsSuccess);
            await using ICanGatewaySession session = openResult.Value!;
            CanFrame frame = CanFrame.CreateClassic(
                0x321,
                isExtendedIdentifier: false,
                new byte[] { 0x10, 0x20, 0x30 });

            HardwareOperationResult transmitResult =
                await session.TransmitAsync(CanGatewaySide.Tx, frame);

            Assert.IsTrue(transmitResult.IsSuccess);
            Assert.AreEqual(2UL, api.LastTransmitAccessMask);
            Assert.AreEqual(0x321u, api.LastTransmitIdentifier);
            Assert.AreEqual(false, api.LastTransmitIsExtendedIdentifier);
            CollectionAssert.AreEqual(
                new byte[] { 0x10, 0x20, 0x30 },
                api.LastTransmitData);
            Assert.AreEqual(VectorCanInterfaceVersion.Version3, api.OpenedInterfaceVersion);
            Assert.AreEqual(3UL, api.OpenedAccessMask);
        }

        [TestMethod]
        public async Task Classic_session_receives_a_standard_frame_from_the_RX_channel()
        {
            var api = new FakeVectorXlApi();
            api.ReceiveEvents.Enqueue(new VectorClassicCanEvent(
                channelIndex: 0,
                rawIdentifier: 0x123,
                dataLength: 3,
                flags: VectorClassicCanEventFlags.None,
                data: new byte[] { 0xAA, 0xBB, 0xCC },
                timestampNanoseconds: 8_000));
            var service = new VectorHardwareService(() => api);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());
            Assert.IsTrue(openResult.IsSuccess);
            await using ICanGatewaySession session = openResult.Value!;

            RoutedCanFrame received = await ReadNextAsync(session.ReceiveAsync());

            Assert.AreEqual(CanGatewaySide.Rx, received.Source);
            Assert.AreEqual(0x123u, received.Frame.Identifier);
            Assert.IsFalse(received.Frame.IsExtendedIdentifier);
            CollectionAssert.AreEqual(
                new byte[] { 0xAA, 0xBB, 0xCC },
                received.Frame.Data.ToArray());
        }

        [TestMethod]
        public async Task Classic_session_receives_an_extended_frame_from_the_TX_channel()
        {
            var api = new FakeVectorXlApi();
            api.ReceiveEvents.Enqueue(new VectorClassicCanEvent(
                channelIndex: 1,
                rawIdentifier: 0x98DAF110,
                dataLength: 4,
                flags: VectorClassicCanEventFlags.None,
                data: new byte[] { 0x01, 0x02, 0x03, 0x04 },
                timestampNanoseconds: 16_000));
            var service = new VectorHardwareService(() => api);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());
            Assert.IsTrue(openResult.IsSuccess);
            await using ICanGatewaySession session = openResult.Value!;

            RoutedCanFrame received = await ReadNextAsync(session.ReceiveAsync());

            Assert.AreEqual(CanGatewaySide.Tx, received.Source);
            Assert.AreEqual(0x18DAF110u, received.Frame.Identifier);
            Assert.IsTrue(received.Frame.IsExtendedIdentifier);
            CollectionAssert.AreEqual(
                new byte[] { 0x01, 0x02, 0x03, 0x04 },
                received.Frame.Data.ToArray());
        }

        [TestMethod]
        public async Task Classic_session_flushes_native_receive_and_transmit_queues()
        {
            var api = new FakeVectorXlApi();
            var service = new VectorHardwareService(() => api);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());
            Assert.IsTrue(openResult.IsSuccess);
            await using ICanGatewaySession session = openResult.Value!;

            HardwareOperationResult flushResult = await session.FlushAsync();

            Assert.IsTrue(flushResult.IsSuccess);
            Assert.AreEqual(1, api.FlushReceiveCallCount);
            Assert.AreEqual(1, api.FlushTransmitCallCount);
            Assert.AreEqual(3UL, api.LastFlushTransmitAccessMask);
        }

        [TestMethod]
        public async Task Classic_receive_failure_throws_typed_native_hardware_error()
        {
            var api = new FakeVectorXlApi
            {
                ReceiveStatus = new VectorNativeStatus(201, "XL_ERR_INVALID_PORTHANDLE")
            };
            var service = new VectorHardwareService(() => api);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());
            Assert.IsTrue(openResult.IsSuccess);
            await using ICanGatewaySession session = openResult.Value!;
            await using IAsyncEnumerator<RoutedCanFrame> enumerator =
                session.ReceiveAsync().GetAsyncEnumerator();

            HardwareOperationException exception =
                await Assert.ThrowsExceptionAsync<HardwareOperationException>(
                    async () => await enumerator.MoveNextAsync().AsTask());

            Assert.AreEqual(HardwareOperation.Receive, exception.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.ReceiveFailed, exception.Failure.Code);
            Assert.AreEqual(201, exception.Failure.NativeStatus);
            StringAssert.Contains(exception.Failure.Message, "XL_Receive");
            StringAssert.Contains(exception.Failure.Message, "XL_ERR_INVALID_PORTHANDLE");
        }

        [TestMethod]
        public async Task Classic_receive_queue_overrun_throws_a_typed_data_loss_error()
        {
            var api = new FakeVectorXlApi();
            api.ReceiveEvents.Enqueue(new VectorClassicCanEvent(
                channelIndex: 0,
                rawIdentifier: 0x123,
                dataLength: 1,
                flags: VectorClassicCanEventFlags.QueueOverrun,
                data: new byte[] { 0x01 },
                timestampNanoseconds: 8_000));
            var service = new VectorHardwareService(() => api);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());
            Assert.IsTrue(openResult.IsSuccess);
            await using ICanGatewaySession session = openResult.Value!;
            await using IAsyncEnumerator<RoutedCanFrame> enumerator =
                session.ReceiveAsync().GetAsyncEnumerator();

            HardwareOperationException exception =
                await Assert.ThrowsExceptionAsync<HardwareOperationException>(
                    async () => await enumerator.MoveNextAsync().AsTask());

            Assert.AreEqual(HardwareOperation.Receive, exception.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.ReceiveFailed, exception.Failure.Code);
            StringAssert.Contains(exception.Failure.Message, "overrun");
        }

        [TestMethod]
        public async Task Classic_receive_rejects_a_native_event_with_an_invalid_DLC()
        {
            var api = new FakeVectorXlApi();
            api.ReceiveEvents.Enqueue(new VectorClassicCanEvent(
                channelIndex: 0,
                rawIdentifier: 0x123,
                dataLength: 9,
                flags: VectorClassicCanEventFlags.None,
                data: new byte[9],
                timestampNanoseconds: 8_000));
            var service = new VectorHardwareService(() => api);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());
            Assert.IsTrue(openResult.IsSuccess);
            await using ICanGatewaySession session = openResult.Value!;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await using IAsyncEnumerator<RoutedCanFrame> enumerator =
                session.ReceiveAsync(timeout.Token).GetAsyncEnumerator();

            HardwareOperationException exception =
                await Assert.ThrowsExceptionAsync<HardwareOperationException>(
                    async () => await enumerator.MoveNextAsync().AsTask());

            Assert.AreEqual(HardwareOperation.Receive, exception.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.ReceiveFailed, exception.Failure.Code);
            StringAssert.Contains(exception.Failure.Message, "DLC");
        }

        [TestMethod]
        public async Task Classic_receive_honors_caller_cancellation_while_the_queue_is_empty()
        {
            var api = new FakeVectorXlApi();
            var service = new VectorHardwareService(() => api);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());
            Assert.IsTrue(openResult.IsSuccess);
            await using ICanGatewaySession session = openResult.Value!;
            using var cancellation = new CancellationTokenSource();
            await using IAsyncEnumerator<RoutedCanFrame> enumerator =
                session.ReceiveAsync(cancellation.Token).GetAsyncEnumerator();

            Task<bool> pendingReceive = enumerator.MoveNextAsync().AsTask();
            cancellation.Cancel();

            try
            {
                await pendingReceive.WaitAsync(TimeSpan.FromSeconds(1));
                Assert.Fail("The pending receive completed without observing cancellation.");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // TaskCanceledException is also a valid cancellation outcome.
            }
        }

        [TestMethod]
        public async Task Classic_transmit_failure_returns_typed_native_error()
        {
            var api = new FakeVectorXlApi
            {
                TransmitStatus = new VectorNativeStatus(11, "XL_ERR_QUEUE_IS_FULL")
            };
            var service = new VectorHardwareService(() => api);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());
            Assert.IsTrue(openResult.IsSuccess);
            await using ICanGatewaySession session = openResult.Value!;
            CanFrame frame = CanFrame.CreateClassic(
                0x456,
                isExtendedIdentifier: false,
                new byte[] { 0x5A });

            HardwareOperationResult result =
                await session.TransmitAsync(CanGatewaySide.Rx, frame);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Failure);
            Assert.AreEqual(HardwareOperation.Transmit, result.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.TransmitFailed, result.Failure.Code);
            Assert.AreEqual(11, result.Failure.NativeStatus);
            StringAssert.Contains(result.Failure.Message, "XL_CanTransmit");
            StringAssert.Contains(result.Failure.Message, "XL_ERR_QUEUE_IS_FULL");
            Assert.AreEqual(1UL, api.LastTransmitAccessMask);
        }

        [TestMethod]
        public async Task Classic_flush_failure_reports_both_native_queue_statuses()
        {
            var api = new FakeVectorXlApi
            {
                FlushReceiveStatus = new VectorNativeStatus(201, "XL_ERR_INVALID_PORTHANDLE"),
                FlushTransmitStatus = new VectorNativeStatus(11, "XL_ERR_QUEUE_IS_FULL")
            };
            var service = new VectorHardwareService(() => api);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());
            Assert.IsTrue(openResult.IsSuccess);
            await using ICanGatewaySession session = openResult.Value!;

            HardwareOperationResult result = await session.FlushAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Failure);
            Assert.AreEqual(HardwareOperation.Flush, result.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.FlushFailed, result.Failure.Code);
            Assert.AreEqual(201, result.Failure.NativeStatus);
            StringAssert.Contains(result.Failure.Message, "XL_FlushReceiveQueue");
            StringAssert.Contains(result.Failure.Message, "XL_ERR_INVALID_PORTHANDLE");
            StringAssert.Contains(result.Failure.Message, "XL_CanFlushTransmitQueue");
            StringAssert.Contains(result.Failure.Message, "XL_ERR_QUEUE_IS_FULL");
            Assert.AreEqual(1, api.FlushReceiveCallCount);
            Assert.AreEqual(1, api.FlushTransmitCallCount);
        }

        [TestMethod]
        public async Task Classic_session_rejects_a_CAN_FD_frame_before_calling_the_native_API()
        {
            var api = new FakeVectorXlApi();
            var service = new VectorHardwareService(() => api);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());
            Assert.IsTrue(openResult.IsSuccess);
            await using ICanGatewaySession session = openResult.Value!;
            CanFrame frame = CanFrame.CreateFlexibleDataRate(
                0x123,
                isExtendedIdentifier: false,
                CanDataLengthCode.Bytes12,
                isBitRateSwitchEnabled: true,
                new byte[12]);

            HardwareOperationResult result =
                await session.TransmitAsync(CanGatewaySide.Tx, frame);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Failure);
            Assert.AreEqual(HardwareOperation.Transmit, result.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.InvalidConfiguration, result.Failure.Code);
            Assert.IsNull(api.LastTransmitAccessMask);
        }

        [TestMethod]
        public async Task Classic_session_rejects_an_unknown_destination_before_calling_the_native_API()
        {
            var api = new FakeVectorXlApi();
            var service = new VectorHardwareService(() => api);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());
            Assert.IsTrue(openResult.IsSuccess);
            await using ICanGatewaySession session = openResult.Value!;
            CanFrame frame = CanFrame.CreateClassic(
                0x123,
                isExtendedIdentifier: false,
                new byte[] { 0x01 });

            HardwareOperationResult result =
                await session.TransmitAsync((CanGatewaySide)99, frame);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Failure);
            Assert.AreEqual(HardwareOperation.Transmit, result.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.InvalidConfiguration, result.Failure.Code);
            Assert.IsNull(api.LastTransmitAccessMask);
        }

        [TestMethod]
        public async Task Stopping_a_Classic_session_completes_a_pending_receive()
        {
            var api = new FakeVectorXlApi();
            var service = new VectorHardwareService(() => api);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await service.OpenGatewaySessionAsync(CreateClassicOptions());
            Assert.IsTrue(openResult.IsSuccess);
            ICanGatewaySession session = openResult.Value!;
            await using IAsyncEnumerator<RoutedCanFrame> enumerator =
                session.ReceiveAsync().GetAsyncEnumerator();
            Task<bool> pendingReceive = enumerator.MoveNextAsync().AsTask();

            HardwareOperationResult stopResult = await session.StopAsync();
            bool receivedFrame = await pendingReceive.WaitAsync(TimeSpan.FromSeconds(1));
            await session.DisposeAsync();

            Assert.IsTrue(stopResult.IsSuccess);
            Assert.IsFalse(receivedFrame);
        }

        [TestMethod]
        public void Classic_options_reject_the_same_physical_channel_for_RX_and_TX()
        {
            var channel = new HardwareChannel
            {
                Name = "CH1",
                ChannelIndex = 0,
                ChannelMask = 1
            };

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => CanGatewayOptions.CreateClassic(channel, channel, 500_000));

            StringAssert.Contains(exception.Message, "different hardware channels");
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

        private static async Task<T> ReadNextAsync<T>(IAsyncEnumerable<T> source)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await using IAsyncEnumerator<T> enumerator =
                source.GetAsyncEnumerator(timeout.Token);

            if (await enumerator.MoveNextAsync())
            {
                return enumerator.Current;
            }

            throw new AssertFailedException(
                "The asynchronous sequence completed before yielding an item.");
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

            public VectorNativeStatus TransmitStatus { get; init; } =
                new VectorNativeStatus(0, "XL_SUCCESS");

            public VectorNativeStatus ReceiveStatus { get; init; } =
                new VectorNativeStatus(10, "XL_ERR_QUEUE_IS_EMPTY");

            public VectorNativeStatus FlushReceiveStatus { get; init; } =
                new VectorNativeStatus(0, "XL_SUCCESS");

            public VectorNativeStatus FlushTransmitStatus { get; init; } =
                new VectorNativeStatus(0, "XL_SUCCESS");

            public bool IsDriverOpen { get; private set; }

            public bool IsPortOpen { get; private set; }

            public bool AreChannelsActive { get; private set; }

            public ulong? GrantedPermissionMask { get; init; }

            public bool ReturnPortHandleOnOpenFailure { get; init; }

            public VectorCanInterfaceVersion? OpenedInterfaceVersion { get; private set; }

            public ulong? OpenedAccessMask { get; private set; }

            public IReadOnlyList<VectorChannelDescriptor> Channels { get; init; } =
                Array.Empty<VectorChannelDescriptor>();

            public Queue<VectorClassicCanEvent> ReceiveEvents { get; } = new();

            public ulong? LastTransmitAccessMask { get; private set; }

            public uint? LastTransmitIdentifier { get; private set; }

            public bool? LastTransmitIsExtendedIdentifier { get; private set; }

            public byte[]? LastTransmitData { get; private set; }

            public int FlushReceiveCallCount { get; private set; }

            public int FlushTransmitCallCount { get; private set; }

            public ulong? LastFlushTransmitAccessMask { get; private set; }

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
                OpenedAccessMask = accessMask;
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

            public VectorNativeStatus TransmitClassicCanFrame(
                int portHandle,
                ulong accessMask,
                uint identifier,
                bool isExtendedIdentifier,
                ReadOnlyMemory<byte> data)
            {
                LastTransmitAccessMask = accessMask;
                LastTransmitIdentifier = identifier;
                LastTransmitIsExtendedIdentifier = isExtendedIdentifier;
                LastTransmitData = data.ToArray();
                return TransmitStatus;
            }

            public VectorClassicReceiveBatchResult ReceiveClassicCanEvents(
                int portHandle,
                int maximumEventCount)
            {
                var events = new List<VectorClassicCanEvent>();
                while (events.Count < maximumEventCount && ReceiveEvents.TryDequeue(out VectorClassicCanEvent? receivedEvent))
                {
                    events.Add(receivedEvent);
                }

                return new VectorClassicReceiveBatchResult(
                    ReceiveStatus,
                    events);
            }

            public VectorNativeStatus FlushReceiveQueue(int portHandle)
            {
                FlushReceiveCallCount++;
                return FlushReceiveStatus;
            }

            public VectorNativeStatus FlushClassicTransmitQueue(
                int portHandle,
                ulong accessMask)
            {
                FlushTransmitCallCount++;
                LastFlushTransmitAccessMask = accessMask;
                return FlushTransmitStatus;
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
