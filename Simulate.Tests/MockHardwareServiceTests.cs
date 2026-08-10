using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class MockHardwareServiceTests
    {
        [TestMethod]
        public async Task Gateway_session_routes_received_RX_frame_and_exposes_transmission_to_TX()
        {
            var driver = new MockHardwareService();
            HardwareOperationResult<IReadOnlyList<HardwareInterface>> discovery =
                await driver.DiscoverInterfacesAsync();

            Assert.IsTrue(discovery.IsSuccess);
            IReadOnlyList<HardwareInterface> interfaces = discovery.Value!;
            HardwareChannel rxChannel = interfaces[0].Channels[0];
            HardwareChannel txChannel = interfaces[0].Channels[1];
            CanGatewayOptions options = CanGatewayOptions.CreateClassic(rxChannel, txChannel, 500000);

            HardwareOperationResult<ICanGatewaySession> openResult =
                await driver.OpenGatewaySessionAsync(options);

            Assert.IsTrue(openResult.IsSuccess);
            Assert.IsInstanceOfType<MockCanGatewaySession>(openResult.Value);
            var session = (MockCanGatewaySession)openResult.Value!;
            CanFrame inboundFrame = CanFrame.CreateClassic(0x123, isExtendedIdentifier: false, new byte[] { 0x10, 0x20 });

            HardwareOperationResult enqueueResult = await session.EnqueueReceivedAsync(CanGatewaySide.Rx, inboundFrame);

            Assert.IsTrue(enqueueResult.IsSuccess);
            RoutedCanFrame received = await ReadNextAsync(session.ReceiveAsync());
            Assert.AreEqual(CanGatewaySide.Rx, received.Source);
            Assert.AreEqual(0x123u, received.Frame.Identifier);
            CollectionAssert.AreEqual(new byte[] { 0x10, 0x20 }, received.Frame.Data.ToArray());

            HardwareOperationResult transmitResult = await session.TransmitAsync(CanGatewaySide.Tx, received.Frame);

            Assert.IsTrue(transmitResult.IsSuccess);
            RoutedCanFrame transmitted = await ReadNextAsync(session.ReceiveTransmittedAsync());
            Assert.AreEqual(CanGatewaySide.Tx, transmitted.Source);
            Assert.AreEqual(received.Frame.Identifier, transmitted.Frame.Identifier);
            CollectionAssert.AreEqual(received.Frame.Data.ToArray(), transmitted.Frame.Data.ToArray());
        }

        [TestMethod]
        public async Task Gateway_session_routes_received_TX_frame_and_exposes_transmission_to_RX()
        {
            MockCanGatewaySession session = await OpenSessionAsync(new MockHardwareService());
            CanFrame inboundFrame = CanFrame.CreateClassic(0x321, isExtendedIdentifier: false, new byte[] { 0x30, 0x40 });

            HardwareOperationResult enqueueResult = await session.EnqueueReceivedAsync(CanGatewaySide.Tx, inboundFrame);

            Assert.IsTrue(enqueueResult.IsSuccess);
            RoutedCanFrame received = await ReadNextAsync(session.ReceiveAsync());
            Assert.AreEqual(CanGatewaySide.Tx, received.Source);
            Assert.AreEqual(0x321u, received.Frame.Identifier);

            HardwareOperationResult transmitResult = await session.TransmitAsync(CanGatewaySide.Rx, received.Frame);

            Assert.IsTrue(transmitResult.IsSuccess);
            RoutedCanFrame transmitted = await ReadNextAsync(session.ReceiveTransmittedAsync());
            Assert.AreEqual(CanGatewaySide.Rx, transmitted.Source);
            CollectionAssert.AreEqual(new byte[] { 0x30, 0x40 }, transmitted.Frame.Data.ToArray());
        }

        [TestMethod]
        public async Task Discovery_returns_the_configured_typed_failure()
        {
            var driver = new MockHardwareService(
                new MockHardwareFaultPlan(MockHardwareFaultPoint.DiscoverInterfaces));

            HardwareOperationResult<IReadOnlyList<HardwareInterface>> result =
                await driver.DiscoverInterfacesAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Failure);
            Assert.AreEqual(HardwareOperation.DiscoverInterfaces, result.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.DiscoveryFailed, result.Failure.Code);
        }

        [DataTestMethod]
        [DataRow(MockHardwareFaultPoint.OpenDriver, HardwareOperation.OpenDriver, HardwareErrorCode.DriverUnavailable)]
        [DataRow(MockHardwareFaultPoint.OpenSession, HardwareOperation.OpenSession, HardwareErrorCode.OpenFailed)]
        [DataRow(MockHardwareFaultPoint.ConfigureSession, HardwareOperation.ConfigureSession, HardwareErrorCode.ConfigurationFailed)]
        [DataRow(MockHardwareFaultPoint.ActivateSession, HardwareOperation.ActivateSession, HardwareErrorCode.ActivationFailed)]
        public async Task Open_gateway_returns_the_configured_setup_failure(
            MockHardwareFaultPoint faultPoint,
            HardwareOperation expectedOperation,
            HardwareErrorCode expectedErrorCode)
        {
            var driver = new MockHardwareService(new MockHardwareFaultPlan(faultPoint));
            HardwareOperationResult<IReadOnlyList<HardwareInterface>> discovery =
                await driver.DiscoverInterfacesAsync();
            IReadOnlyList<HardwareInterface> interfaces = discovery.Value!;
            CanGatewayOptions options = CanGatewayOptions.CreateClassic(
                interfaces[0].Channels[0],
                interfaces[0].Channels[1],
                500000);

            HardwareOperationResult<ICanGatewaySession> result =
                await driver.OpenGatewaySessionAsync(options);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Failure);
            Assert.AreEqual(expectedOperation, result.Failure.Operation);
            Assert.AreEqual(expectedErrorCode, result.Failure.Code);
        }

        [TestMethod]
        public async Task Transmit_returns_the_configured_typed_failure()
        {
            var driver = new MockHardwareService(
                new MockHardwareFaultPlan(MockHardwareFaultPoint.Transmit));
            MockCanGatewaySession session = await OpenSessionAsync(driver);
            CanFrame frame = CanFrame.CreateClassic(0x456, isExtendedIdentifier: false, new byte[] { 0xAA });

            HardwareOperationResult result = await session.TransmitAsync(CanGatewaySide.Tx, frame);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Failure);
            Assert.AreEqual(HardwareOperation.Transmit, result.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.TransmitFailed, result.Failure.Code);
        }

        [TestMethod]
        public async Task Classic_session_rejects_a_CAN_FD_frame()
        {
            MockCanGatewaySession session = await OpenSessionAsync(new MockHardwareService());
            CanFrame frame = CanFrame.CreateFlexibleDataRate(
                0x123,
                isExtendedIdentifier: false,
                CanDataLengthCode.Bytes12,
                isBitRateSwitchEnabled: true,
                new byte[12]);

            HardwareOperationResult result = await session.TransmitAsync(CanGatewaySide.Tx, frame);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Failure);
            Assert.AreEqual(HardwareErrorCode.InvalidConfiguration, result.Failure.Code);
        }

        [TestMethod]
        public async Task Stop_and_dispose_are_idempotent_and_prevent_future_transmission()
        {
            MockCanGatewaySession session = await OpenSessionAsync(new MockHardwareService());
            CanFrame frame = CanFrame.CreateClassic(0x789, isExtendedIdentifier: false, new byte[] { 0x01 });

            HardwareOperationResult flushBeforeStop = await session.FlushAsync();
            HardwareOperationResult firstStop = await session.StopAsync();
            HardwareOperationResult secondStop = await session.StopAsync();
            HardwareOperationResult transmitAfterStop = await session.TransmitAsync(CanGatewaySide.Tx, frame);
            await session.DisposeAsync();
            await session.DisposeAsync();
            await using IAsyncEnumerator<RoutedCanFrame> emittedFrames =
                session.ReceiveTransmittedAsync().GetAsyncEnumerator();
            bool emittedAfterStop = await emittedFrames.MoveNextAsync();

            Assert.IsTrue(flushBeforeStop.IsSuccess);
            Assert.IsTrue(firstStop.IsSuccess);
            Assert.IsTrue(secondStop.IsSuccess);
            Assert.IsFalse(session.IsOpen);
            Assert.IsFalse(transmitAfterStop.IsSuccess);
            Assert.IsNotNull(transmitAfterStop.Failure);
            Assert.AreEqual(HardwareErrorCode.SessionNotOpen, transmitAfterStop.Failure.Code);
            Assert.IsFalse(emittedAfterStop);
        }

        private static async Task<MockCanGatewaySession> OpenSessionAsync(MockHardwareService driver)
        {
            HardwareOperationResult<IReadOnlyList<HardwareInterface>> discovery =
                await driver.DiscoverInterfacesAsync();
            Assert.IsTrue(discovery.IsSuccess);
            IReadOnlyList<HardwareInterface> interfaces = discovery.Value!;
            CanGatewayOptions options = CanGatewayOptions.CreateClassic(
                interfaces[0].Channels[0],
                interfaces[0].Channels[1],
                500000);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await driver.OpenGatewaySessionAsync(options);

            Assert.IsTrue(openResult.IsSuccess);
            Assert.IsInstanceOfType<MockCanGatewaySession>(openResult.Value);
            return (MockCanGatewaySession)openResult.Value!;
        }

        private static async Task<T> ReadNextAsync<T>(IAsyncEnumerable<T> source)
        {
            using var timeout = new CancellationTokenSource(System.TimeSpan.FromSeconds(1));
            await using IAsyncEnumerator<T> enumerator = source.GetAsyncEnumerator(timeout.Token);

            if (await enumerator.MoveNextAsync())
            {
                return enumerator.Current;
            }

            throw new AssertFailedException("The asynchronous sequence completed before yielding an item.");
        }
    }
}
