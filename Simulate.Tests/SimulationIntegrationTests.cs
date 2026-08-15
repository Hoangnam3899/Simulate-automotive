using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class SimulationIntegrationTests
    {
        [TestMethod]
        public async Task Classic_connection_routes_pass_block_and_inject_then_disconnects()
        {
            MockHardwareService driver = new();
            await using MockCanGatewaySession session = await OpenSessionAsync(
                driver,
                CanBusMode.Classic);
            await using var engine = new SimulationEngine(
                session,
                CreateClassicRoutingPlan());

            await engine.StartAsync();

            CanFrame passThroughFrame = CanFrame.CreateClassic(
                0x123,
                isExtendedIdentifier: false,
                new byte[] { 0x10, 0x20 });
            CanFrame blockedFrame = CanFrame.CreateClassic(
                0x124,
                isExtendedIdentifier: false,
                new byte[] { 0x30 });
            CanFrame injectFrame = CanFrame.CreateClassic(
                0x125,
                isExtendedIdentifier: false,
                new byte[] { 0x01, 0x20 });

            Assert.IsTrue((await session.EnqueueReceivedAsync(
                CanGatewaySide.Rx,
                passThroughFrame)).IsSuccess);
            RoutedCanFrame passThroughTransmission =
                await ReadNextAsync(session.ReceiveTransmittedAsync());
            Assert.AreEqual(CanGatewaySide.Tx, passThroughTransmission.Source);
            CollectionAssert.AreEqual(
                passThroughFrame.Data.ToArray(),
                passThroughTransmission.Frame.Data.ToArray());

            Assert.IsTrue((await session.EnqueueReceivedAsync(
                CanGatewaySide.Rx,
                blockedFrame)).IsSuccess);
            Assert.IsTrue((await session.EnqueueReceivedAsync(
                CanGatewaySide.Rx,
                injectFrame)).IsSuccess);
            RoutedCanFrame injectTransmission =
                await ReadNextAsync(session.ReceiveTransmittedAsync());

            Assert.AreEqual((uint)0x125, injectTransmission.Frame.Identifier);
            CollectionAssert.AreEqual(
                new byte[] { 0x55, 0x20 },
                injectTransmission.Frame.Data.ToArray());

            await WaitUntilAsync(() => engine.Statistics.ReceivedFrames == 3);
            GatewayStatistics statistics = engine.Statistics;
            Assert.AreEqual(3L, statistics.ReceivedFrames);
            Assert.AreEqual(2L, statistics.TransmittedFrames);
            Assert.AreEqual(1L, statistics.PassedFrames);
            Assert.AreEqual(1L, statistics.DroppedFrames);
            Assert.AreEqual(1L, statistics.InjectedFrames);
            Assert.AreEqual(CanBusMode.Classic, session.Options.BusMode);

            await engine.StopAsync();
            HardwareOperationResult stopResult = await session.StopAsync();

            Assert.IsTrue(stopResult.IsSuccess);
            Assert.IsFalse(engine.IsRunning);
            Assert.IsFalse(session.IsOpen);

            HardwareOperationResult afterStopEnqueue = await session.EnqueueReceivedAsync(
                CanGatewaySide.Rx,
                passThroughFrame);
            Assert.IsFalse(afterStopEnqueue.IsSuccess);
            Assert.AreEqual(HardwareErrorCode.SessionNotOpen, afterStopEnqueue.Failure!.Code);

            await using IAsyncEnumerator<RoutedCanFrame> remainingTransmissions =
                session.ReceiveTransmittedAsync().GetAsyncEnumerator();
            Assert.IsFalse(await remainingTransmissions.MoveNextAsync());
        }

        [TestMethod]
        public async Task Flexible_data_rate_connection_runs_a_scheduled_injected_frame_then_disconnects()
        {
            MockHardwareService driver = new();
            await using MockCanGatewaySession session = await OpenSessionAsync(
                driver,
                CanBusMode.FlexibleDataRate);
            await using var engine = new SimulationEngine(
                session,
                CreateFlexibleDataRatePlan());

            await engine.StartAsync();
            await engine.StartSchedulingAsync();

            RoutedCanFrame transmitted =
                await ReadNextAsync(session.ReceiveTransmittedAsync());

            Assert.AreEqual(CanGatewaySide.Tx, transmitted.Source);
            Assert.AreEqual((uint)0x126, transmitted.Frame.Identifier);
            Assert.AreEqual(CanFrameFormat.FlexibleDataRate, transmitted.Frame.Format);
            Assert.AreEqual(CanDataLengthCode.Bytes12, transmitted.Frame.DataLengthCode);
            Assert.IsTrue(transmitted.Frame.IsBitRateSwitchEnabled);
            CollectionAssert.AreEqual(
                new byte[] { 0x5A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                transmitted.Frame.Data.ToArray());

            await WaitUntilAsync(() => engine.Statistics.ScheduledFrames == 1);
            Assert.AreEqual(1L, engine.Statistics.ScheduledFrames);
            Assert.AreEqual(1L, engine.Statistics.TransmittedFrames);
            Assert.AreEqual(1L, engine.Statistics.InjectedFrames);
            Assert.AreEqual(CanBusMode.FlexibleDataRate, session.Options.BusMode);

            await engine.StopSchedulingAsync();
            await engine.StopAsync();
            HardwareOperationResult stopResult = await session.StopAsync();

            Assert.IsTrue(stopResult.IsSuccess);
            Assert.IsFalse(engine.IsScheduling);
            Assert.IsFalse(engine.IsRunning);
            Assert.IsFalse(session.IsOpen);
        }

        [TestMethod]
        public async Task Soak_connect_start_stop_disconnect_releases_each_in_memory_run()
        {
            MockHardwareService driver = new();
            SimulationPlan plan = CreateClassicRoutingPlan();

            for (int cycle = 0; cycle < 50; cycle++)
            {
                await using MockCanGatewaySession session = await OpenSessionAsync(
                    driver,
                    CanBusMode.Classic);
                await using var engine = new SimulationEngine(session, plan);

                await engine.StartAsync();
                await engine.StartSchedulingAsync();
                CanFrame frame = CanFrame.CreateClassic(
                    0x456,
                    isExtendedIdentifier: false,
                    new byte[] { (byte)cycle });
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    frame)).IsSuccess);

                RoutedCanFrame firstTransmission =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());
                RoutedCanFrame secondTransmission =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());
                uint[] transmissionIdentifiers =
                [
                    firstTransmission.Frame.Identifier,
                    secondTransmission.Frame.Identifier
                ];
                CollectionAssert.AreEquivalent(
                    new uint[] { 0x456, 0x125 },
                    transmissionIdentifiers);

                await engine.StopAsync();
                long transmittedBeforePostStopInput = engine.Statistics.TransmittedFrames;

                Assert.IsFalse(engine.IsRunning, $"Receive worker leaked on soak cycle {cycle}.");
                Assert.IsFalse(engine.IsScheduling, $"Scheduler leaked on soak cycle {cycle}.");
                Assert.IsTrue(session.IsOpen, $"Engine unexpectedly closed session on soak cycle {cycle}.");

                HardwareOperationResult afterEngineStopEnqueue = await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    frame);
                Assert.IsTrue(afterEngineStopEnqueue.IsSuccess);
                await AssertNoNextAsync(session.ReceiveTransmittedAsync());
                Assert.AreEqual(
                    transmittedBeforePostStopInput,
                    engine.Statistics.TransmittedFrames,
                    $"Engine emitted a frame after stop on soak cycle {cycle}.");

                HardwareOperationResult stopResult = await session.StopAsync();
                Assert.IsTrue(stopResult.IsSuccess);
                Assert.IsFalse(session.IsOpen, $"Session remained open on soak cycle {cycle}.");

                HardwareOperationResult afterSessionStopEnqueue = await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    frame);
                Assert.IsFalse(afterSessionStopEnqueue.IsSuccess);
                await using IAsyncEnumerator<RoutedCanFrame> remainingTransmissions =
                    session.ReceiveTransmittedAsync().GetAsyncEnumerator();
                Assert.IsFalse(await remainingTransmissions.MoveNextAsync());
            }
        }

        [TestMethod]
        public async Task Transmit_failure_stops_cleanly_and_allows_same_driver_reconnect()
        {
            var driver = new FailOnceTransmitHardwareDriver();
            await using MockCanGatewaySession failedSession = await OpenSessionAsync(
                driver,
                CanBusMode.Classic);
            await using var failedEngine = new SimulationEngine(
                failedSession,
                CreateClassicRoutingPlan());

            await failedEngine.StartAsync();
            Assert.IsTrue((await failedSession.EnqueueReceivedAsync(
                CanGatewaySide.Rx,
                CanFrame.CreateClassic(0x456, false, new byte[] { 0xAA }))).IsSuccess);
            await WaitUntilAsync(() =>
                failedEngine.Statistics.ReceivedFrames == 1 && !failedEngine.IsRunning);

            HardwareFailure? rootFailure = failedEngine.LastFailure;
            Assert.IsNotNull(rootFailure);
            Assert.AreEqual(HardwareOperation.Transmit, rootFailure.Operation);
            Assert.AreEqual(HardwareErrorCode.TransmitFailed, rootFailure.Code);

            HardwareOperationException? transmitFailure = null;
            try
            {
                await failedEngine.StopAsync();
            }
            catch (HardwareOperationException exception)
            {
                transmitFailure = exception;
            }

            Assert.IsNotNull(transmitFailure);
            Assert.AreEqual(HardwareOperation.Transmit, transmitFailure!.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.TransmitFailed, transmitFailure.Failure.Code);
            Assert.AreSame(rootFailure, transmitFailure.Failure);
            Assert.AreSame(rootFailure, failedEngine.LastFailure);
            Assert.AreEqual(0L, failedEngine.Statistics.TransmittedFrames);
            Assert.IsNull(failedEngine.Statistics.LastRoutingLatency);
            Assert.IsTrue(failedSession.IsOpen);

            Assert.IsTrue((await failedSession.StopAsync()).IsSuccess);
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => failedEngine.StartAsync().AsTask());
            Assert.AreSame(rootFailure, failedEngine.LastFailure);

            await failedEngine.DisposeAsync();
            await failedSession.DisposeAsync();
            Assert.IsFalse(failedSession.IsOpen);

            await using MockCanGatewaySession healthySession = await OpenSessionAsync(
                driver,
                CanBusMode.Classic);
            Assert.AreEqual(2, driver.OpenCount);
            await using var healthyEngine = new SimulationEngine(
                healthySession,
                CreateClassicRoutingPlan());

            await healthyEngine.StartAsync();
            CanFrame reconnectFrame = CanFrame.CreateClassic(
                0x456,
                isExtendedIdentifier: false,
                new byte[] { 0x55 });
            Assert.IsTrue((await healthySession.EnqueueReceivedAsync(
                CanGatewaySide.Rx,
                reconnectFrame)).IsSuccess);
            RoutedCanFrame reconnectTransmission =
                await ReadNextAsync(healthySession.ReceiveTransmittedAsync());

            Assert.AreEqual((uint)0x456, reconnectTransmission.Frame.Identifier);
            CollectionAssert.AreEqual(
                reconnectFrame.Data.ToArray(),
                reconnectTransmission.Frame.Data.ToArray());

            await healthyEngine.StopAsync();
            Assert.IsTrue((await healthySession.StopAsync()).IsSuccess);
            Assert.IsFalse(healthyEngine.IsRunning);
            Assert.IsFalse(healthySession.IsOpen);
        }

        private static SimulationPlan CreateClassicRoutingPlan()
        {
            const string documentText = """
                BO_ 292 Blocked: 1 Gateway
                 SG_ Value : 0|8@1+ (1,0) [0|255] "" Gateway
                BO_ 293 Injected: 2 Gateway
                 SG_ Value : 0|8@1+ (1,0) [0|255] "" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
            var blockedRule = new SimulationMessageRule(
                canIdentifier: 0x124,
                isExtendedIdentifier: false,
                isEnabled: true,
                GatewayMode.Block,
                SimulationSendType.OneShot,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                signalOverrides: [],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            var injectRule = new SimulationMessageRule(
                canIdentifier: 0x125,
                isExtendedIdentifier: false,
                isEnabled: true,
                GatewayMode.Inject,
                SimulationSendType.OneShot,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                signalOverrides: [new SignalOverride("Value", 0x55)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));

            return new SimulationPlan(document, [blockedRule, injectRule]);
        }

        private static SimulationPlan CreateFlexibleDataRatePlan()
        {
            const string documentText = """
                BO_ 294 FlexibleStatus: 12 Gateway
                 SG_ Value : 0|8@1+ (1,0) [0|255] "" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
            var rule = new SimulationMessageRule(
                canIdentifier: 0x126,
                isExtendedIdentifier: false,
                isEnabled: true,
                GatewayMode.Inject,
                SimulationSendType.OneShot,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                signalOverrides: [new SignalOverride("Value", 0x5A)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));

            return new SimulationPlan(document, [rule]);
        }

        private static async Task<MockCanGatewaySession> OpenSessionAsync(
            ICanHardwareDriver driver,
            CanBusMode busMode)
        {
            HardwareOperationResult<IReadOnlyList<HardwareInterface>> discovery =
                await driver.DiscoverInterfacesAsync();
            Assert.IsTrue(discovery.IsSuccess);
            HardwareInterface hardwareInterface = discovery.Value![0];
            HardwareChannel rxChannel = hardwareInterface.Channels[0];
            HardwareChannel txChannel = hardwareInterface.Channels[1];
            CanGatewayOptions options = busMode == CanBusMode.Classic
                ? CanGatewayOptions.CreateClassic(rxChannel, txChannel, 500000)
                : CanGatewayOptions.CreateFlexibleDataRate(
                    rxChannel,
                    txChannel,
                    500000,
                    2000000);

            HardwareOperationResult<ICanGatewaySession> openResult =
                await driver.OpenGatewaySessionAsync(options);
            Assert.IsTrue(openResult.IsSuccess);
            Assert.IsInstanceOfType<MockCanGatewaySession>(openResult.Value);
            return (MockCanGatewaySession)openResult.Value!;
        }

        private static async Task<T> ReadNextAsync<T>(IAsyncEnumerable<T> source)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await using IAsyncEnumerator<T> enumerator = source.GetAsyncEnumerator(timeout.Token);
            if (await enumerator.MoveNextAsync())
            {
                return enumerator.Current;
            }

            throw new AssertFailedException(
                "The asynchronous sequence completed before yielding an item.");
        }

        private static async Task AssertNoNextAsync<T>(IAsyncEnumerable<T> source)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await using IAsyncEnumerator<T> enumerator = source.GetAsyncEnumerator(timeout.Token);
            try
            {
                Assert.IsFalse(
                    await enumerator.MoveNextAsync(),
                    "The source yielded a frame after the engine stopped.");
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                // The open in-memory session remains idle, so bounded cancellation is the expected result.
            }
        }

        private static async Task WaitUntilAsync(System.Func<bool> predicate)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            while (!predicate())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1), timeout.Token);
            }
        }

        private sealed class FailOnceTransmitHardwareDriver : ICanHardwareDriver
        {
            private readonly MockHardwareService _discoveryDriver = new();
            private int _openCount;

            public int OpenCount => Volatile.Read(ref _openCount);

            public Task<HardwareOperationResult<IReadOnlyList<HardwareInterface>>>
                DiscoverInterfacesAsync(CancellationToken cancellationToken = default)
            {
                return _discoveryDriver.DiscoverInterfacesAsync(cancellationToken);
            }

            public Task<HardwareOperationResult<ICanGatewaySession>> OpenGatewaySessionAsync(
                CanGatewayOptions options,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int openCount = Interlocked.Increment(ref _openCount);
                var faultPlan = openCount == 1
                    ? new MockHardwareFaultPlan(MockHardwareFaultPoint.Transmit)
                    : new MockHardwareFaultPlan();
                ICanGatewaySession session = new MockCanGatewaySession(options, faultPlan);
                return Task.FromResult(HardwareOperationResult.Succeeded(session));
            }
        }
    }
}
