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
        public async Task Start_injects_only_overridden_signal_bits_into_the_live_fd_baseline()
        {
            const string documentText = """
                BO_ 2147483939 ExtendedData: 12 Gateway
                 SG_ ScaledValue : 0|4@1+ (0.5,-1) [-1|6.5] "" Gateway
                 SG_ UntouchedValue : 4|4@1+ (1,0) [0|15] "" Gateway
                 SG_ SignedValue : 8|4@1- (2,0) [-16|14] "" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
            var rule = new SimulationMessageRule(
                canIdentifier: 0x123,
                isExtendedIdentifier: true,
                isEnabled: true,
                GatewayMode.Inject,
                SimulationSendType.OneShot,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                signalOverrides:
                [
                    new SignalOverride("ScaledValue", 2d),
                    new SignalOverride("SignedValue", -4d)
                ],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            var plan = new SimulationPlan(document, [rule]);
            await using MockCanGatewaySession session = await OpenSessionAsync(CanBusMode.FlexibleDataRate);
            var engine = new SimulationEngine(session, plan);
            byte[] livePayload = [0xA5, 0x51, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB];
            CanFrame inboundFrame = CanFrame.CreateFlexibleDataRate(
                0x123,
                isExtendedIdentifier: true,
                CanDataLengthCode.Bytes12,
                isBitRateSwitchEnabled: true,
                livePayload);

            await engine.StartAsync();
            try
            {
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    inboundFrame)).IsSuccess);
                RoutedCanFrame transmitted =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.AreEqual(CanGatewaySide.Tx, transmitted.Source);
                Assert.AreEqual((uint)0x123, transmitted.Frame.Identifier);
                Assert.IsTrue(transmitted.Frame.IsExtendedIdentifier);
                Assert.AreEqual(CanFrameFormat.FlexibleDataRate, transmitted.Frame.Format);
                Assert.AreEqual(CanDataLengthCode.Bytes12, transmitted.Frame.DataLengthCode);
                Assert.IsTrue(transmitted.Frame.IsBitRateSwitchEnabled);
                CollectionAssert.AreEqual(
                    new byte[] { 0xA6, 0x5E, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB },
                    transmitted.Frame.Data.ToArray());
                Assert.AreEqual(1L, engine.Statistics.InjectedFrames);
                Assert.AreEqual(0L, engine.Statistics.PassedFrames);
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Replace_signal_overrides_publishes_only_valid_complete_snapshots_while_running()
        {
            const string documentText = """
                BO_ 291 Status: 1 Gateway
                 SG_ Mode : 0|4@1+ (1,0) [0|100] "" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
            var rule = new SimulationMessageRule(
                canIdentifier: 0x123,
                isExtendedIdentifier: false,
                isEnabled: true,
                GatewayMode.Inject,
                SimulationSendType.OneShot,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                signalOverrides: [new SignalOverride("Mode", 1d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            await using MockCanGatewaySession session = await OpenSessionAsync();
            ISimulationEngine engine = new SimulationEngine(
                session,
                new SimulationPlan(document, [rule]));

            await engine.StartAsync();
            try
            {
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    CanFrame.CreateClassic(0x123, false, new byte[] { 0xA0 }))).IsSuccess);
                RoutedCanFrame first = await ReadNextAsync(session.ReceiveTransmittedAsync());

                engine.ReplaceSignalOverrides(
                    canIdentifier: 0x123,
                    isExtendedIdentifier: false,
                    signalOverrides: [new SignalOverride("Mode", 7d)]);
                Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                    engine.ReplaceSignalOverrides(
                        canIdentifier: 0x123,
                        isExtendedIdentifier: false,
                        signalOverrides: [new SignalOverride("Mode", 20d)]));

                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    CanFrame.CreateClassic(0x123, false, new byte[] { 0xB0 }))).IsSuccess);
                RoutedCanFrame second = await ReadNextAsync(session.ReceiveTransmittedAsync());

                engine.ReplaceSignalOverrides(
                    canIdentifier: 0x123,
                    isExtendedIdentifier: false,
                    signalOverrides: []);
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    CanFrame.CreateClassic(0x123, false, new byte[] { 0xC4 }))).IsSuccess);
                RoutedCanFrame third = await ReadNextAsync(session.ReceiveTransmittedAsync());

                CollectionAssert.AreEqual(new byte[] { 0xA1 }, first.Frame.Data.ToArray());
                CollectionAssert.AreEqual(new byte[] { 0xB7 }, second.Frame.Data.ToArray());
                CollectionAssert.AreEqual(new byte[] { 0xC4 }, third.Frame.Data.ToArray());
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Create_rejects_an_initial_override_that_does_not_fit_the_signal_bits()
        {
            const string documentText = """
                BO_ 291 Status: 1 Gateway
                 SG_ Mode : 0|4@1+ (1,0) [0|100] "" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
            var rule = new SimulationMessageRule(
                canIdentifier: 0x123,
                isExtendedIdentifier: false,
                isEnabled: true,
                GatewayMode.Inject,
                SimulationSendType.OneShot,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                signalOverrides: [new SignalOverride("Mode", 20d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            var plan = new SimulationPlan(document, [rule]);
            await using MockCanGatewaySession session = await OpenSessionAsync();

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => new SimulationEngine(session, plan));
        }

        [TestMethod]
        public async Task Concurrent_override_replacement_never_mixes_values_from_two_snapshots()
        {
            const string documentText = """
                BO_ 291 Status: 2 Gateway
                 SG_ First : 0|8@1+ (1,0) [0|255] "" Gateway
                 SG_ Second : 8|8@1+ (1,0) [0|255] "" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
            var rule = new SimulationMessageRule(
                canIdentifier: 0x123,
                isExtendedIdentifier: false,
                isEnabled: true,
                GatewayMode.Inject,
                SimulationSendType.OneShot,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                signalOverrides:
                [
                    new SignalOverride("First", 1d),
                    new SignalOverride("Second", 10d)
                ],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            await using MockCanGatewaySession session = await OpenSessionAsync();
            ISimulationEngine engine = new SimulationEngine(
                session,
                new SimulationPlan(document, [rule]));

            await engine.StartAsync();
            try
            {
                Task updater = Task.Run(() =>
                {
                    for (int index = 0; index < 500; index++)
                    {
                        bool even = index % 2 == 0;
                        engine.ReplaceSignalOverrides(
                            canIdentifier: 0x123,
                            isExtendedIdentifier: false,
                            signalOverrides:
                            [
                                new SignalOverride("First", even ? 2d : 3d),
                                new SignalOverride("Second", even ? 20d : 30d)
                            ]);
                    }
                });

                for (int index = 0; index < 200; index++)
                {
                    Assert.IsTrue((await session.EnqueueReceivedAsync(
                        CanGatewaySide.Rx,
                        CanFrame.CreateClassic(0x123, false, new byte[] { 0x00, 0x00 }))).IsSuccess);
                }

                await updater;
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await using IAsyncEnumerator<RoutedCanFrame> transmittedFrames =
                    session.ReceiveTransmittedAsync(timeout.Token).GetAsyncEnumerator();
                for (int index = 0; index < 200; index++)
                {
                    Assert.IsTrue(await transmittedFrames.MoveNextAsync());
                    byte[] payload = transmittedFrames.Current.Frame.Data.ToArray();
                    bool isCompleteSnapshot =
                        (payload[0] == 1 && payload[1] == 10)
                        || (payload[0] == 2 && payload[1] == 20)
                        || (payload[0] == 3 && payload[1] == 30);
                    Assert.IsTrue(
                        isCompleteSnapshot,
                        $"Mixed override snapshot detected: [{payload[0]}, {payload[1]}].");
                }
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Start_applies_e2e_after_packing_signal_overrides()
        {
            const string documentText = """
                BO_ 291 ProtectedData: 8 Gateway
                 SG_ Command : 16|8@1+ (1,0) [0|255] "" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
            var rule = new SimulationMessageRule(
                canIdentifier: 0x123,
                isExtendedIdentifier: false,
                isEnabled: true,
                GatewayMode.Inject,
                SimulationSendType.OneShot,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                signalOverrides: [new SignalOverride("Command", 9d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: true));
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var engine = new SimulationEngine(
                session,
                new SimulationPlan(document, [rule]));
            CanFrame inboundFrame = CanFrame.CreateClassic(
                0x123,
                isExtendedIdentifier: false,
                new byte[] { 0xAA, 0xF0, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });

            await engine.StartAsync();
            try
            {
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    inboundFrame)).IsSuccess);
                RoutedCanFrame transmitted =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                CollectionAssert.AreEqual(
                    new byte[] { 0xA8, 0xF0, 0x09, 0x02, 0x03, 0x04, 0x05, 0x06 },
                    transmitted.Frame.Data.ToArray());
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Start_filters_one_matching_echo_received_from_the_transmit_side()
        {
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var engine = new SimulationEngine(session, CreatePlan());
            CanFrame firstFrame = CanFrame.CreateClassic(
                0x456,
                isExtendedIdentifier: false,
                new byte[] { 0x10, 0x20 });
            CanFrame followingFrame = CanFrame.CreateClassic(
                0x457,
                isExtendedIdentifier: false,
                new byte[] { 0x30, 0x40 });

            await engine.StartAsync();
            try
            {
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    firstFrame)).IsSuccess);
                RoutedCanFrame firstTransmission =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());
                Assert.AreEqual(CanGatewaySide.Tx, firstTransmission.Source);

                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Tx,
                    firstTransmission.Frame)).IsSuccess);
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Tx,
                    followingFrame)).IsSuccess);
                RoutedCanFrame nextTransmission =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.AreEqual(CanGatewaySide.Rx, nextTransmission.Source);
                Assert.AreEqual(followingFrame.Identifier, nextTransmission.Frame.Identifier);
                CollectionAssert.AreEqual(
                    followingFrame.Data.ToArray(),
                    nextTransmission.Frame.Data.ToArray());

                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Tx,
                    firstTransmission.Frame)).IsSuccess);
                RoutedCanFrame repeatedRealFrame =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());
                Assert.AreEqual(firstFrame.Identifier, repeatedRealFrame.Frame.Identifier);
                Assert.AreEqual(1L, engine.Statistics.FilteredEchoFrames);
                Assert.AreEqual(4L, engine.Statistics.ReceivedFrames);
                Assert.AreEqual(3L, engine.Statistics.TransmittedFrames);
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Start_filters_a_matching_echo_in_the_reverse_gateway_direction()
        {
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var engine = new SimulationEngine(session, CreatePlan());
            CanFrame frame = CanFrame.CreateClassic(
                0x456,
                isExtendedIdentifier: false,
                new byte[] { 0x10 });
            CanFrame followingFrame = CanFrame.CreateClassic(
                0x457,
                isExtendedIdentifier: false,
                new byte[] { 0x20 });

            await engine.StartAsync();
            try
            {
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Tx,
                    frame)).IsSuccess);
                RoutedCanFrame firstTransmission =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());
                Assert.AreEqual(CanGatewaySide.Rx, firstTransmission.Source);

                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    firstTransmission.Frame)).IsSuccess);
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    followingFrame)).IsSuccess);
                RoutedCanFrame nextTransmission =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.AreEqual(CanGatewaySide.Tx, nextTransmission.Source);
                Assert.AreEqual(followingFrame.Identifier, nextTransmission.Frame.Identifier);
                Assert.AreEqual(1L, engine.Statistics.FilteredEchoFrames);
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Start_forwards_an_identical_real_frame_after_the_echo_window_expires()
        {
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var timeProvider = new ManualTimeProvider();
            var engine = new SimulationEngine(
                session,
                CreatePlan(),
                new SimulationEngineOptions(
                    echoWindow: TimeSpan.FromMilliseconds(10),
                    maximumPendingEchoes: 32),
                timeProvider);
            CanFrame frame = CanFrame.CreateClassic(
                0x456,
                isExtendedIdentifier: false,
                new byte[] { 0x10, 0x20 });

            await engine.StartAsync();
            try
            {
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    frame)).IsSuccess);
                RoutedCanFrame firstTransmission =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                timeProvider.Advance(TimeSpan.FromMilliseconds(11));
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Tx,
                    firstTransmission.Frame)).IsSuccess);
                RoutedCanFrame secondTransmission =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.AreEqual(CanGatewaySide.Rx, secondTransmission.Source);
                CollectionAssert.AreEqual(
                    frame.Data.ToArray(),
                    secondTransmission.Frame.Data.ToArray());
                Assert.AreEqual(0L, engine.Statistics.FilteredEchoFrames);
                Assert.AreEqual(2L, engine.Statistics.TransmittedFrames);
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Echo_filter_evicts_the_oldest_entry_at_its_configured_bound()
        {
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var engine = new SimulationEngine(
                session,
                CreatePlan(),
                new SimulationEngineOptions(
                    echoWindow: TimeSpan.FromMilliseconds(10),
                    maximumPendingEchoes: 1),
                new ManualTimeProvider());
            CanFrame oldestFrame = CanFrame.CreateClassic(
                0x456,
                isExtendedIdentifier: false,
                new byte[] { 0x10 });
            CanFrame newestFrame = CanFrame.CreateClassic(
                0x457,
                isExtendedIdentifier: false,
                new byte[] { 0x20 });

            await engine.StartAsync();
            try
            {
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    oldestFrame)).IsSuccess);
                RoutedCanFrame oldestTransmission =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    newestFrame)).IsSuccess);
                _ = await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Tx,
                    oldestTransmission.Frame)).IsSuccess);
                RoutedCanFrame forwardedOldest =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.AreEqual(CanGatewaySide.Rx, forwardedOldest.Source);
                Assert.AreEqual(oldestFrame.Identifier, forwardedOldest.Frame.Identifier);
                Assert.AreEqual(0L, engine.Statistics.FilteredEchoFrames);
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Echo_filter_matches_the_modified_frame_emitted_by_injection()
        {
            const string documentText = """
                BO_ 291 Status: 1 Gateway
                 SG_ Mode : 0|4@1+ (1,0) [0|15] "" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
            var rule = new SimulationMessageRule(
                canIdentifier: 0x123,
                isExtendedIdentifier: false,
                isEnabled: true,
                GatewayMode.Inject,
                SimulationSendType.OneShot,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                signalOverrides: [new SignalOverride("Mode", 5d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var engine = new SimulationEngine(
                session,
                new SimulationPlan(document, [rule]));

            await engine.StartAsync();
            try
            {
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    CanFrame.CreateClassic(0x123, false, new byte[] { 0xA0 }))).IsSuccess);
                RoutedCanFrame injected =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());
                CollectionAssert.AreEqual(new byte[] { 0xA5 }, injected.Frame.Data.ToArray());

                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Tx,
                    injected.Frame)).IsSuccess);
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Tx,
                    CanFrame.CreateClassic(0x124, false, new byte[] { 0xB0 }))).IsSuccess);
                RoutedCanFrame nextTransmission =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.AreEqual((uint)0x124, nextTransmission.Frame.Identifier);
                Assert.AreEqual(1L, engine.Statistics.FilteredEchoFrames);
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

        private static Task<MockCanGatewaySession> OpenSessionAsync()
        {
            return OpenSessionAsync(CanBusMode.Classic);
        }

        private static async Task<MockCanGatewaySession> OpenSessionAsync(CanBusMode busMode)
        {
            var driver = new MockHardwareService();
            HardwareOperationResult<IReadOnlyList<HardwareInterface>> discovery =
                await driver.DiscoverInterfacesAsync();
            Assert.IsTrue(discovery.IsSuccess);
            IReadOnlyList<HardwareInterface> interfaces = discovery.Value!;
            CanGatewayOptions options = busMode == CanBusMode.Classic
                ? CanGatewayOptions.CreateClassic(
                    interfaces[0].Channels[0],
                    interfaces[0].Channels[1],
                    500000)
                : CanGatewayOptions.CreateFlexibleDataRate(
                    interfaces[0].Channels[0],
                    interfaces[0].Channels[1],
                    nominalBitrate: 500000,
                    dataBitrate: 2000000);
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

        private sealed class ManualTimeProvider : TimeProvider
        {
            private long _timestamp;

            public override long TimestampFrequency => TimeSpan.TicksPerSecond;

            public override long GetTimestamp()
            {
                return Interlocked.Read(ref _timestamp);
            }

            public void Advance(TimeSpan elapsed)
            {
                Interlocked.Add(ref _timestamp, elapsed.Ticks);
            }
        }
    }
}
