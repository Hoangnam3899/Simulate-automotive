using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class SimulationEngineTests
    {
        [TestMethod]
        public async Task Start_routes_an_unconfigured_RX_frame_to_TX()
        {
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var engine = new SimulationEngine(session, CreatePlan());
            CanFrame inboundFrame = CanFrame.CreateClassic(
                0x456,
                isExtendedIdentifier: false,
                new byte[] { 0x10, 0x20 });

            await engine.StartAsync();
            try
            {
                HardwareOperationResult enqueueResult =
                    await session.EnqueueReceivedAsync(CanGatewaySide.Rx, inboundFrame);
                RoutedCanFrame transmitted =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.IsTrue(enqueueResult.IsSuccess);
                Assert.AreEqual(CanGatewaySide.Tx, transmitted.Source);
                Assert.AreEqual(inboundFrame.Identifier, transmitted.Frame.Identifier);
                Assert.AreEqual(inboundFrame.IsExtendedIdentifier, transmitted.Frame.IsExtendedIdentifier);
                Assert.AreEqual(inboundFrame.Format, transmitted.Frame.Format);
                Assert.AreEqual(inboundFrame.DataLengthCode, transmitted.Frame.DataLengthCode);
                CollectionAssert.AreEqual(
                    inboundFrame.Data.ToArray(),
                    transmitted.Frame.Data.ToArray());

                GatewayStatistics statistics = engine.Statistics;
                Assert.AreEqual(1L, statistics.ReceivedFrames);
                Assert.AreEqual(1L, statistics.PassedFrames);
                Assert.AreEqual(1L, statistics.TransmittedFrames);
                Assert.AreEqual(0L, statistics.DroppedFrames);
            }
            finally
            {
                await engine.StopAsync();
            }

            Assert.IsFalse(engine.IsRunning);
            Assert.IsTrue(session.IsOpen);
        }

        [TestMethod]
        public async Task Start_routes_a_TX_frame_when_its_block_rule_is_disabled()
        {
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var engine = new SimulationEngine(
                session,
                CreatePlan(isEnabled: false, GatewayMode.Block));
            CanFrame inboundFrame = CanFrame.CreateClassic(
                0x123,
                isExtendedIdentifier: false,
                new byte[] { 0x30, 0x40 });

            await engine.StartAsync();
            try
            {
                HardwareOperationResult enqueueResult =
                    await session.EnqueueReceivedAsync(CanGatewaySide.Tx, inboundFrame);
                RoutedCanFrame transmitted =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.IsTrue(enqueueResult.IsSuccess);
                Assert.AreEqual(CanGatewaySide.Rx, transmitted.Source);
                Assert.AreEqual(inboundFrame.Identifier, transmitted.Frame.Identifier);
                CollectionAssert.AreEqual(
                    inboundFrame.Data.ToArray(),
                    transmitted.Frame.Data.ToArray());

                GatewayStatistics statistics = engine.Statistics;
                Assert.AreEqual(1L, statistics.ReceivedFrames);
                Assert.AreEqual(1L, statistics.PassedFrames);
                Assert.AreEqual(1L, statistics.TransmittedFrames);
                Assert.AreEqual(0L, statistics.DroppedFrames);
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Start_routes_a_frame_matching_an_enabled_pass_through_rule()
        {
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var engine = new SimulationEngine(
                session,
                CreatePlan(isEnabled: true, GatewayMode.PassThrough));
            CanFrame inboundFrame = CanFrame.CreateClassic(
                0x123,
                isExtendedIdentifier: false,
                new byte[] { 0x50, 0x60 });

            await engine.StartAsync();
            try
            {
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    inboundFrame)).IsSuccess);
                RoutedCanFrame transmitted =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.AreEqual(CanGatewaySide.Tx, transmitted.Source);
                Assert.AreEqual(inboundFrame.Identifier, transmitted.Frame.Identifier);
                CollectionAssert.AreEqual(
                    inboundFrame.Data.ToArray(),
                    transmitted.Frame.Data.ToArray());
                Assert.AreEqual(1L, engine.Statistics.PassedFrames);
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Start_blocks_an_enabled_rule_once_without_transmitting_it()
        {
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var engine = new SimulationEngine(
                session,
                CreatePlan(isEnabled: true, GatewayMode.Block));
            CanFrame blockedFrame = CanFrame.CreateClassic(
                0x123,
                isExtendedIdentifier: false,
                new byte[] { 0xAA });
            CanFrame followingFrame = CanFrame.CreateClassic(
                0x124,
                isExtendedIdentifier: false,
                new byte[] { 0xBB });

            await engine.StartAsync();
            try
            {
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    blockedFrame)).IsSuccess);
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    followingFrame)).IsSuccess);

                RoutedCanFrame transmitted =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.AreEqual(followingFrame.Identifier, transmitted.Frame.Identifier);
                GatewayStatistics statistics = engine.Statistics;
                Assert.AreEqual(2L, statistics.ReceivedFrames);
                Assert.AreEqual(1L, statistics.PassedFrames);
                Assert.AreEqual(1L, statistics.TransmittedFrames);
                Assert.AreEqual(1L, statistics.DroppedFrames);
            }
            finally
            {
                await engine.StopAsync();
            }

            Assert.IsTrue((await session.StopAsync()).IsSuccess);
            await using IAsyncEnumerator<RoutedCanFrame> remainingFrames =
                session.ReceiveTransmittedAsync().GetAsyncEnumerator();
            Assert.IsFalse(await remainingFrames.MoveNextAsync());
        }

        [TestMethod]
        public async Task Stop_completes_while_the_receive_loop_is_idle_and_keeps_the_session_open()
        {
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var engine = new SimulationEngine(session, CreatePlan());

            await engine.StartAsync();

            Assert.IsTrue(engine.IsRunning);
            await engine.StopAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

            Assert.IsFalse(engine.IsRunning);
            Assert.IsTrue(session.IsOpen);
            Assert.AreEqual(0L, engine.Statistics.ReceivedFrames);
        }

        [TestMethod]
        public async Task Concurrent_stop_calls_complete_during_start_token_cancellation()
        {
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var engine = new SimulationEngine(session, CreatePlan());
            using var runCancellation = new CancellationTokenSource();

            await engine.StartAsync(runCancellation.Token);
            runCancellation.Cancel();
            Task firstStop = engine.StopAsync().AsTask();
            Task secondStop = engine.StopAsync().AsTask();

            await Task.WhenAll(firstStop, secondStop).WaitAsync(TimeSpan.FromSeconds(1));

            Assert.IsFalse(engine.IsRunning);
            Assert.IsTrue(session.IsOpen);
        }

        [TestMethod]
        public async Task Idle_receive_stream_is_enumerated_once_without_polling()
        {
            MockCanGatewaySession innerSession = await OpenSessionAsync();
            await using var session = new CountingReceiveGatewaySession(innerSession);
            var engine = new SimulationEngine(session, CreatePlan());

            await engine.StartAsync();
            await session.ReceiveStarted.WaitAsync(TimeSpan.FromSeconds(1));
            await Task.Delay(TimeSpan.FromMilliseconds(50));

            Assert.AreEqual(1, session.ReceiveEnumerationCount);
            await engine.StopAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
            Assert.IsFalse(engine.IsRunning);
        }

        private static SimulationPlan CreatePlan()
        {
            const string documentText = """
                BO_ 291 VehicleData: 2 Gateway
                 SG_ VehicleSpeed : 0|8@1+ (0.5,0) [0|127.5] "km/h" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");

            return new SimulationPlan(document, messageRules: []);
        }

        private static SimulationPlan CreatePlan(bool isEnabled, GatewayMode gatewayMode)
        {
            const string documentText = """
                BO_ 291 VehicleData: 2 Gateway
                 SG_ VehicleSpeed : 0|8@1+ (0.5,0) [0|127.5] "km/h" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
            var rule = new SimulationMessageRule(
                canIdentifier: 0x123,
                isExtendedIdentifier: false,
                isEnabled,
                gatewayMode,
                SimulationSendType.OneShot,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                signalOverrides: [],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));

            return new SimulationPlan(document, [rule]);
        }

        private static async Task<MockCanGatewaySession> OpenSessionAsync()
        {
            var driver = new MockHardwareService();
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

            throw new AssertFailedException(
                "The asynchronous sequence completed before yielding an item.");
        }

        private sealed class CountingReceiveGatewaySession : ICanGatewaySession
        {
            private readonly MockCanGatewaySession _innerSession;
            private readonly TaskCompletionSource _receiveStarted = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            private int _receiveEnumerationCount;

            public CountingReceiveGatewaySession(MockCanGatewaySession innerSession)
            {
                _innerSession = innerSession;
            }

            public CanGatewayOptions Options => _innerSession.Options;

            public bool IsOpen => _innerSession.IsOpen;

            public int ReceiveEnumerationCount =>
                Volatile.Read(ref _receiveEnumerationCount);

            public Task ReceiveStarted => _receiveStarted.Task;

            public async IAsyncEnumerable<RoutedCanFrame> ReceiveAsync(
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _receiveEnumerationCount);
                _receiveStarted.TrySetResult();

                await foreach (RoutedCanFrame frame in
                    _innerSession.ReceiveAsync(cancellationToken))
                {
                    yield return frame;
                }
            }

            public ValueTask<HardwareOperationResult> TransmitAsync(
                CanGatewaySide destination,
                CanFrame frame,
                CancellationToken cancellationToken = default)
            {
                return _innerSession.TransmitAsync(destination, frame, cancellationToken);
            }

            public ValueTask<HardwareOperationResult> FlushAsync(
                CancellationToken cancellationToken = default)
            {
                return _innerSession.FlushAsync(cancellationToken);
            }

            public ValueTask<HardwareOperationResult> StopAsync()
            {
                return _innerSession.StopAsync();
            }

            public ValueTask DisposeAsync()
            {
                return _innerSession.DisposeAsync();
            }
        }
    }
}
