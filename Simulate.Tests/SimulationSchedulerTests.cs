using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class SimulationSchedulerTests
    {
        [TestMethod]
        public async Task One_shot_transmits_one_injected_frame_when_scheduling_starts()
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
            var engine = new SimulationEngine(session, new SimulationPlan(document, [rule]));

            await engine.StartAsync();
            try
            {
                await engine.StartSchedulingAsync();
                RoutedCanFrame transmitted =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.AreEqual(CanGatewaySide.Tx, transmitted.Source);
                Assert.AreEqual((uint)0x123, transmitted.Frame.Identifier);
                CollectionAssert.AreEqual(
                    new byte[] { 0x05 },
                    transmitted.Frame.Data.ToArray());
                Assert.AreEqual(1L, engine.Statistics.ScheduledFrames);
                Assert.AreEqual(1L, engine.Statistics.TransmittedFrames);
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Cyclic_honors_start_delay_cycle_interval_and_finite_repeat_count()
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
                SimulationSendType.Cyclic,
                new SimulationTiming(
                    startDelay: TimeSpan.FromMilliseconds(10),
                    cycleInterval: TimeSpan.FromMilliseconds(5),
                    repeatCount: 3),
                signalOverrides: [new SignalOverride("Mode", 7d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var timeProvider = new ManualTimeProvider();
            var engine = new SimulationEngine(
                session,
                new SimulationPlan(document, [rule]),
                SimulationEngineOptions.Default,
                timeProvider);

            await engine.StartAsync();
            try
            {
                await engine.StartSchedulingAsync();
                await timeProvider.WaitForTimerCountAsync(1);
                Assert.AreEqual(0L, engine.Statistics.ScheduledFrames);

                timeProvider.Advance(TimeSpan.FromMilliseconds(10));
                _ = await ReadNextAsync(session.ReceiveTransmittedAsync());
                await timeProvider.WaitForTimerCountAsync(2);

                timeProvider.Advance(TimeSpan.FromMilliseconds(5));
                _ = await ReadNextAsync(session.ReceiveTransmittedAsync());
                await timeProvider.WaitForTimerCountAsync(3);

                timeProvider.Advance(TimeSpan.FromMilliseconds(5));
                RoutedCanFrame third =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                CollectionAssert.AreEqual(new byte[] { 0x07 }, third.Frame.Data.ToArray());
                Assert.AreEqual(3L, engine.Statistics.ScheduledFrames);
                Assert.AreEqual(3L, engine.Statistics.TransmittedFrames);
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Event_trigger_transmits_immediately_and_debounces_repeated_changes()
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
                SimulationSendType.Event,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 0),
                signalOverrides: [new SignalOverride("Mode", 9d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var timeProvider = new ManualTimeProvider();
            var engine = new SimulationEngine(
                session,
                new SimulationPlan(document, [rule]),
                SimulationEngineOptions.Default,
                timeProvider);

            await engine.StartAsync();
            try
            {
                await engine.StartSchedulingAsync();

                SimulationEventTriggerResult first =
                    await engine.TriggerEventAsync(0x123, isExtendedIdentifier: false);
                RoutedCanFrame transmitted =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());
                SimulationEventTriggerResult debounced =
                    await engine.TriggerEventAsync(0x123, isExtendedIdentifier: false);

                Assert.AreEqual(SimulationEventTriggerResult.Transmitted, first);
                Assert.AreEqual(SimulationEventTriggerResult.Debounced, debounced);
                CollectionAssert.AreEqual(new byte[] { 0x09 }, transmitted.Frame.Data.ToArray());
                Assert.AreEqual(1L, engine.Statistics.ScheduledFrames);

                timeProvider.Advance(TimeSpan.FromMilliseconds(51));
                SimulationEventTriggerResult afterWindow =
                    await engine.TriggerEventAsync(0x123, isExtendedIdentifier: false);
                _ = await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.AreEqual(SimulationEventTriggerResult.Transmitted, afterWindow);
                Assert.AreEqual(2L, engine.Statistics.ScheduledFrames);
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Pause_stops_scheduled_sends_but_keeps_gateway_routing_active()
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
                SimulationSendType.Cyclic,
                new SimulationTiming(
                    startDelay: TimeSpan.FromMilliseconds(10),
                    cycleInterval: TimeSpan.FromMilliseconds(5),
                    repeatCount: 0),
                signalOverrides: [new SignalOverride("Mode", 3d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var timeProvider = new ManualTimeProvider();
            var engine = new SimulationEngine(
                session,
                new SimulationPlan(document, [rule]),
                SimulationEngineOptions.Default,
                timeProvider);

            await engine.StartAsync();
            try
            {
                await engine.StartSchedulingAsync();
                await timeProvider.WaitForTimerCountAsync(1);
                engine.PauseScheduling();

                Assert.IsTrue(engine.IsRunning);
                Assert.IsTrue(engine.IsSchedulingPaused);
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    CanFrame.CreateClassic(0x456, false, new byte[] { 0xAA }))).IsSuccess);
                RoutedCanFrame gatewayFrame =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());
                Assert.AreEqual((uint)0x456, gatewayFrame.Frame.Identifier);

                timeProvider.Advance(TimeSpan.FromMilliseconds(10));
                Assert.AreEqual(0L, engine.Statistics.ScheduledFrames);

                engine.ResumeScheduling();
                RoutedCanFrame scheduledFrame =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.IsFalse(engine.IsSchedulingPaused);
                Assert.AreEqual((uint)0x123, scheduledFrame.Frame.Identifier);
                Assert.AreEqual(1L, engine.Statistics.ScheduledFrames);
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Pause_holds_an_event_waiting_behind_a_gateway_transmit_until_resume()
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
                SimulationSendType.Event,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 0),
                signalOverrides: [new SignalOverride("Mode", 6d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            MockCanGatewaySession innerSession = await OpenSessionAsync();
            await using var session = new BlockingTransmitGatewaySession(innerSession);
            var engine = new SimulationEngine(
                session,
                new SimulationPlan(document, [rule]),
                new SimulationEngineOptions(
                    echoWindow: TimeSpan.FromMilliseconds(10),
                    maximumPendingEchoes: 32,
                    eventDebounce: TimeSpan.Zero),
                TimeProvider.System);

            await engine.StartAsync();
            await engine.StartSchedulingAsync();
            try
            {
                Assert.IsTrue((await innerSession.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    CanFrame.CreateClassic(0x456, false, new byte[] { 0xAA }))).IsSuccess);
                await session.TransmitStarted.WaitAsync(TimeSpan.FromSeconds(1));

                Task<SimulationEventTriggerResult> eventTrigger =
                    engine.TriggerEventAsync(0x123, isExtendedIdentifier: false).AsTask();
                engine.PauseScheduling();
                session.ReleaseTransmit();

                RoutedCanFrame gatewayFrame =
                    await ReadNextAsync(innerSession.ReceiveTransmittedAsync());
                Task firstCompletion = await Task.WhenAny(
                    eventTrigger,
                    Task.Delay(TimeSpan.FromMilliseconds(100)));

                Assert.AreEqual((uint)0x456, gatewayFrame.Frame.Identifier);
                Assert.AreNotSame(
                    eventTrigger,
                    firstCompletion,
                    "A scheduled send waiting behind gateway traffic must remain paused.");
                Assert.AreEqual(0L, engine.Statistics.ScheduledFrames);

                engine.ResumeScheduling();
                SimulationEventTriggerResult triggerResult = await eventTrigger;
                RoutedCanFrame scheduledFrame =
                    await ReadNextAsync(innerSession.ReceiveTransmittedAsync());

                Assert.AreEqual(SimulationEventTriggerResult.Transmitted, triggerResult);
                Assert.AreEqual((uint)0x123, scheduledFrame.Frame.Identifier);
                Assert.AreEqual(1L, engine.Statistics.ScheduledFrames);
            }
            finally
            {
                session.ReleaseTransmit();
                engine.ResumeScheduling();
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Stop_scheduling_cancels_cyclic_work_but_keeps_gateway_active()
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
                SimulationSendType.Cyclic,
                new SimulationTiming(
                    startDelay: TimeSpan.Zero,
                    cycleInterval: TimeSpan.FromMilliseconds(5),
                    repeatCount: 0),
                signalOverrides: [new SignalOverride("Mode", 4d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var timeProvider = new ManualTimeProvider();
            var engine = new SimulationEngine(
                session,
                new SimulationPlan(document, [rule]),
                SimulationEngineOptions.Default,
                timeProvider);

            await engine.StartAsync();
            try
            {
                await engine.StartSchedulingAsync();
                _ = await ReadNextAsync(session.ReceiveTransmittedAsync());
                await timeProvider.WaitForTimerCountAsync(1);

                await engine.StopSchedulingAsync();
                timeProvider.Advance(TimeSpan.FromMilliseconds(100));

                Assert.IsFalse(engine.IsScheduling);
                Assert.IsTrue(engine.IsRunning);
                Assert.IsTrue(session.IsOpen);
                Assert.AreEqual(1L, engine.Statistics.ScheduledFrames);

                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    CanFrame.CreateClassic(0x456, false, new byte[] { 0xBB }))).IsSuccess);
                RoutedCanFrame gatewayFrame =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());
                Assert.AreEqual((uint)0x456, gatewayFrame.Frame.Identifier);
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Emergency_stop_cancels_scheduler_then_closes_the_gateway_session()
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
                SimulationSendType.Cyclic,
                new SimulationTiming(
                    startDelay: TimeSpan.FromMilliseconds(10),
                    cycleInterval: TimeSpan.FromMilliseconds(5),
                    repeatCount: 0),
                signalOverrides: [new SignalOverride("Mode", 6d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var timeProvider = new ManualTimeProvider();
            var engine = new SimulationEngine(
                session,
                new SimulationPlan(document, [rule]),
                SimulationEngineOptions.Default,
                timeProvider);

            await engine.StartAsync();
            await engine.StartSchedulingAsync();
            await timeProvider.WaitForTimerCountAsync(1);

            HardwareOperationResult result = await engine.EmergencyStopAsync();
            timeProvider.Advance(TimeSpan.FromSeconds(1));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(engine.IsRunning);
            Assert.IsFalse(engine.IsScheduling);
            Assert.IsFalse(session.IsOpen);
            Assert.AreEqual(0L, engine.Statistics.ScheduledFrames);
            HardwareOperationResult enqueueAfterStop = await session.EnqueueReceivedAsync(
                CanGatewaySide.Rx,
                CanFrame.CreateClassic(0x456, false, new byte[] { 0xCC }));
            Assert.IsFalse(enqueueAfterStop.IsSuccess);
        }

        [TestMethod]
        public async Task Emergency_stop_closes_the_session_after_a_scheduler_transmit_fault()
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
                signalOverrides: [new SignalOverride("Mode", 7d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            await using MockCanGatewaySession session = await OpenSessionAsync(
                CanBusMode.Classic,
                new MockHardwareFaultPlan(MockHardwareFaultPoint.Transmit));
            var engine = new SimulationEngine(session, new SimulationPlan(document, [rule]));

            await engine.StartAsync();
            await engine.StartSchedulingAsync();
            await WaitUntilAsync(() => !engine.IsScheduling);

            HardwareOperationException failure =
                await Assert.ThrowsExceptionAsync<HardwareOperationException>(
                    () => engine.EmergencyStopAsync().AsTask());

            Assert.AreEqual(HardwareOperation.Transmit, failure.Failure.Operation);
            Assert.AreEqual(HardwareErrorCode.TransmitFailed, failure.Failure.Code);
            Assert.IsFalse(session.IsOpen, "Emergency cleanup must close a faulted session.");
            Assert.IsFalse(engine.IsRunning);
        }

        [TestMethod]
        public async Task Event_send_uses_the_latest_RX_frame_as_its_live_baseline()
        {
            const string documentText = """
                BO_ 291 Status: 1 Gateway
                 SG_ Mode : 0|4@1+ (1,0) [0|15] "" Gateway
                 SG_ Untouched : 4|4@1+ (1,0) [0|15] "" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
            var rule = new SimulationMessageRule(
                canIdentifier: 0x123,
                isExtendedIdentifier: false,
                isEnabled: true,
                GatewayMode.Inject,
                SimulationSendType.Event,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 0),
                signalOverrides: [new SignalOverride("Mode", 5d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var engine = new SimulationEngine(session, new SimulationPlan(document, [rule]));

            await engine.StartAsync();
            try
            {
                Assert.IsTrue((await session.EnqueueReceivedAsync(
                    CanGatewaySide.Rx,
                    CanFrame.CreateClassic(0x123, false, new byte[] { 0xA0 }))).IsSuccess);
                _ = await ReadNextAsync(session.ReceiveTransmittedAsync());
                await engine.StartSchedulingAsync();

                _ = await engine.TriggerEventAsync(0x123, isExtendedIdentifier: false);
                RoutedCanFrame scheduled =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                CollectionAssert.AreEqual(
                    new byte[] { 0xA5 },
                    scheduled.Frame.Data.ToArray());
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task One_shot_creates_a_valid_extended_FD_baseline_from_DBC_length()
        {
            const string documentText = """
                BO_ 2147483939 ExtendedStatus: 12 Gateway
                 SG_ Mode : 0|4@1+ (1,0) [0|15] "" Gateway
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
                signalOverrides: [new SignalOverride("Mode", 2d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            await using MockCanGatewaySession session =
                await OpenSessionAsync(CanBusMode.FlexibleDataRate);
            var engine = new SimulationEngine(session, new SimulationPlan(document, [rule]));

            await engine.StartAsync();
            try
            {
                await engine.StartSchedulingAsync();
                RoutedCanFrame scheduled =
                    await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.AreEqual((uint)0x123, scheduled.Frame.Identifier);
                Assert.IsTrue(scheduled.Frame.IsExtendedIdentifier);
                Assert.AreEqual(CanFrameFormat.FlexibleDataRate, scheduled.Frame.Format);
                Assert.AreEqual(CanDataLengthCode.Bytes12, scheduled.Frame.DataLengthCode);
                Assert.IsTrue(scheduled.Frame.IsBitRateSwitchEnabled);
                Assert.AreEqual(12, scheduled.Frame.Length);
                Assert.AreEqual((byte)0x02, scheduled.Frame.Data.Span[0]);
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        [TestMethod]
        public async Task Emergency_stop_waits_for_an_in_flight_event_send_before_session_cleanup()
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
                SimulationSendType.Event,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 0),
                signalOverrides: [new SignalOverride("Mode", 8d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            MockCanGatewaySession innerSession = await OpenSessionAsync();
            await using var session = new BlockingTransmitGatewaySession(innerSession);
            var engine = new SimulationEngine(session, new SimulationPlan(document, [rule]));

            await engine.StartAsync();
            await engine.StartSchedulingAsync();
            Task<SimulationEventTriggerResult> triggerTask =
                engine.TriggerEventAsync(0x123, isExtendedIdentifier: false).AsTask();
            await session.TransmitStarted.WaitAsync(TimeSpan.FromSeconds(1));

            Task<HardwareOperationResult> emergencyTask = engine.EmergencyStopAsync().AsTask();
            Task firstCompletion = await Task.WhenAny(
                emergencyTask,
                Task.Delay(TimeSpan.FromMilliseconds(100)));
            bool emergencyWaitedForTransmit = !ReferenceEquals(firstCompletion, emergencyTask);

            session.ReleaseTransmit();
            Exception? triggerFailure = null;
            try
            {
                _ = await triggerTask;
            }
            catch (Exception exception)
            {
                triggerFailure = exception;
            }

            HardwareOperationResult emergencyResult = await emergencyTask;

            Assert.IsTrue(emergencyWaitedForTransmit);
            Assert.IsNull(triggerFailure);
            Assert.IsTrue(emergencyResult.IsSuccess);
            Assert.IsFalse(session.StopCalledBeforeTransmitCompleted);
        }

        [TestMethod]
        public async Task Concurrent_event_triggers_serialize_hardware_transmission()
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
                SimulationSendType.Event,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 0),
                signalOverrides: [new SignalOverride("Mode", 8d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            MockCanGatewaySession innerSession = await OpenSessionAsync();
            await using var session = new BlockingTransmitGatewaySession(innerSession);
            var engine = new SimulationEngine(
                session,
                new SimulationPlan(document, [rule]),
                new SimulationEngineOptions(
                    echoWindow: TimeSpan.FromMilliseconds(10),
                    maximumPendingEchoes: 32,
                    eventDebounce: TimeSpan.Zero),
                TimeProvider.System);

            await engine.StartAsync();
            await engine.StartSchedulingAsync();
            Task<SimulationEventTriggerResult> firstTrigger =
                engine.TriggerEventAsync(0x123, isExtendedIdentifier: false).AsTask();
            await session.TransmitStarted.WaitAsync(TimeSpan.FromSeconds(1));
            Task<SimulationEventTriggerResult> secondTrigger =
                engine.TriggerEventAsync(0x123, isExtendedIdentifier: false).AsTask();

            Task firstCompletion = await Task.WhenAny(
                session.ConcurrentTransmitObserved,
                Task.Delay(TimeSpan.FromMilliseconds(100)));
            bool transmittedConcurrently =
                ReferenceEquals(firstCompletion, session.ConcurrentTransmitObserved);

            session.ReleaseTransmit();
            await Task.WhenAll(firstTrigger, secondTrigger);
            await engine.EmergencyStopAsync();

            Assert.IsFalse(transmittedConcurrently);
            Assert.AreEqual(2L, engine.Statistics.ScheduledFrames);
        }

        [TestMethod]
        public async Task Concurrent_pause_schedule_stop_and_engine_stop_complete_without_deadlock()
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
                SimulationSendType.Event,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 0),
                signalOverrides: [new SignalOverride("Mode", 1d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var engine = new SimulationEngine(session, new SimulationPlan(document, [rule]));

            await engine.StartAsync();
            await engine.StartSchedulingAsync();
            engine.PauseScheduling();

            Task stopScheduling = engine.StopSchedulingAsync().AsTask();
            Task stopEngine = engine.StopAsync().AsTask();
            engine.ResumeScheduling();
            await Task.WhenAll(stopScheduling, stopEngine)
                .WaitAsync(TimeSpan.FromSeconds(1));

            Assert.IsFalse(engine.IsScheduling);
            Assert.IsFalse(engine.IsSchedulingPaused);
            Assert.IsFalse(engine.IsRunning);
            Assert.IsTrue(session.IsOpen);
        }

        [TestMethod]
        public async Task Finite_schedule_can_be_stopped_and_started_again()
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
                signalOverrides: [new SignalOverride("Mode", 1d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            await using MockCanGatewaySession session = await OpenSessionAsync();
            var engine = new SimulationEngine(session, new SimulationPlan(document, [rule]));

            await engine.StartAsync();
            try
            {
                await engine.StartSchedulingAsync();
                _ = await ReadNextAsync(session.ReceiveTransmittedAsync());
                await engine.StopSchedulingAsync();

                await engine.StartSchedulingAsync();
                _ = await ReadNextAsync(session.ReceiveTransmittedAsync());

                Assert.AreEqual(2L, engine.Statistics.ScheduledFrames);
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        private static Task<MockCanGatewaySession> OpenSessionAsync()
        {
            return OpenSessionAsync(CanBusMode.Classic);
        }

        private static async Task<MockCanGatewaySession> OpenSessionAsync(
            CanBusMode busMode,
            MockHardwareFaultPlan? faultPlan = null)
        {
            var driver = new MockHardwareService(faultPlan);
            HardwareOperationResult<IReadOnlyList<HardwareInterface>> discovery =
                await driver.DiscoverInterfacesAsync();
            IReadOnlyList<HardwareInterface> interfaces = discovery.Value
                ?? throw new AssertFailedException("Mock discovery must return interfaces.");
            CanGatewayOptions options = busMode == CanBusMode.Classic
                ? CanGatewayOptions.CreateClassic(
                    interfaces[0].Channels[0],
                    interfaces[0].Channels[1],
                    nominalBitrate: 500000)
                : CanGatewayOptions.CreateFlexibleDataRate(
                    interfaces[0].Channels[0],
                    interfaces[0].Channels[1],
                    nominalBitrate: 500000,
                    dataBitrate: 2000000);
            HardwareOperationResult<ICanGatewaySession> openResult =
                await driver.OpenGatewaySessionAsync(options);

            Assert.IsInstanceOfType<MockCanGatewaySession>(openResult.Value);
            return (MockCanGatewaySession)openResult.Value!;
        }

        private static async Task WaitUntilAsync(Func<bool> predicate)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            while (!predicate())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1), timeout.Token);
            }
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

        private sealed class ManualTimeProvider : TimeProvider
        {
            private readonly object _sync = new();
            private readonly List<ManualTimer> _timers = new();
            private TaskCompletionSource _timerChanged = CreateCompletionSource();
            private long _timestamp;
            private int _timerCount;

            public override long TimestampFrequency => TimeSpan.TicksPerSecond;

            public override long GetTimestamp()
            {
                return Interlocked.Read(ref _timestamp);
            }

            public override ITimer CreateTimer(
                TimerCallback callback,
                object? state,
                TimeSpan dueTime,
                TimeSpan period)
            {
                ArgumentNullException.ThrowIfNull(callback);
                var timer = new ManualTimer(this, callback, state);
                timer.Change(dueTime, period);
                return timer;
            }

            public async Task WaitForTimerCountAsync(int expectedCount)
            {
                while (Volatile.Read(ref _timerCount) < expectedCount)
                {
                    Task changed;
                    lock (_sync)
                    {
                        changed = _timerChanged.Task;
                    }

                    await changed.WaitAsync(TimeSpan.FromSeconds(1));
                }
            }

            public void Advance(TimeSpan elapsed)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
                List<ManualTimer> dueTimers;
                lock (_sync)
                {
                    _timestamp += elapsed.Ticks;
                    dueTimers = _timers
                        .Where(timer => timer.IsDue(_timestamp))
                        .ToList();
                    foreach (ManualTimer timer in dueTimers)
                    {
                        timer.MarkFired(_timestamp);
                    }
                }

                foreach (ManualTimer timer in dueTimers)
                {
                    timer.Invoke();
                }
            }

            private static TaskCompletionSource CreateCompletionSource()
            {
                return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            private void ChangeTimer(
                ManualTimer timer,
                TimeSpan dueTime,
                TimeSpan period)
            {
                lock (_sync)
                {
                    if (!_timers.Contains(timer))
                    {
                        _timers.Add(timer);
                    }

                    timer.SetSchedule(_timestamp, dueTime, period);
                    _timerCount++;
                    TaskCompletionSource changed = _timerChanged;
                    _timerChanged = CreateCompletionSource();
                    changed.TrySetResult();
                }
            }

            private void RemoveTimer(ManualTimer timer)
            {
                lock (_sync)
                {
                    _timers.Remove(timer);
                }
            }

            private sealed class ManualTimer : ITimer
            {
                private readonly ManualTimeProvider _owner;
                private readonly TimerCallback _callback;
                private readonly object? _state;
                private long _dueTimestamp = long.MaxValue;
                private long _periodTicks = Timeout.InfiniteTimeSpan.Ticks;
                private bool _isDisposed;

                public ManualTimer(
                    ManualTimeProvider owner,
                    TimerCallback callback,
                    object? state)
                {
                    _owner = owner;
                    _callback = callback;
                    _state = state;
                }

                public bool Change(TimeSpan dueTime, TimeSpan period)
                {
                    if (_isDisposed)
                    {
                        return false;
                    }

                    _owner.ChangeTimer(this, dueTime, period);
                    return true;
                }

                public void Dispose()
                {
                    _isDisposed = true;
                    _owner.RemoveTimer(this);
                }

                public ValueTask DisposeAsync()
                {
                    Dispose();
                    return ValueTask.CompletedTask;
                }

                public bool IsDue(long timestamp)
                {
                    return !_isDisposed && _dueTimestamp <= timestamp;
                }

                public void MarkFired(long timestamp)
                {
                    _dueTimestamp = _periodTicks == Timeout.InfiniteTimeSpan.Ticks
                        ? long.MaxValue
                        : timestamp + _periodTicks;
                }

                public void Invoke()
                {
                    _callback(_state);
                }

                public void SetSchedule(long timestamp, TimeSpan dueTime, TimeSpan period)
                {
                    _dueTimestamp = dueTime == Timeout.InfiniteTimeSpan
                        ? long.MaxValue
                        : timestamp + dueTime.Ticks;
                    _periodTicks = period.Ticks;
                }
            }
        }

        private sealed class BlockingTransmitGatewaySession : ICanGatewaySession
        {
            private readonly MockCanGatewaySession _innerSession;
            private readonly TaskCompletionSource _transmitStarted = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource _releaseTransmit = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource _concurrentTransmitObserved = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            private int _activeTransmits;
            private int _stopCalledBeforeTransmitCompleted;

            public BlockingTransmitGatewaySession(MockCanGatewaySession innerSession)
            {
                _innerSession = innerSession;
            }

            public CanGatewayOptions Options => _innerSession.Options;

            public bool IsOpen => _innerSession.IsOpen;

            public Task TransmitStarted => _transmitStarted.Task;

            public Task ConcurrentTransmitObserved => _concurrentTransmitObserved.Task;

            public bool StopCalledBeforeTransmitCompleted =>
                Volatile.Read(ref _stopCalledBeforeTransmitCompleted) != 0;

            public IAsyncEnumerable<RoutedCanFrame> ReceiveAsync(
                CancellationToken cancellationToken = default)
            {
                return _innerSession.ReceiveAsync(cancellationToken);
            }

            public async ValueTask<HardwareOperationResult> TransmitAsync(
                CanGatewaySide destination,
                CanFrame frame,
                CancellationToken cancellationToken = default)
            {
                if (Interlocked.Increment(ref _activeTransmits) > 1)
                {
                    _concurrentTransmitObserved.TrySetResult();
                }
                _transmitStarted.TrySetResult();
                try
                {
                    await _releaseTransmit.Task.ConfigureAwait(false);
                    return await _innerSession.TransmitAsync(
                        destination,
                        frame,
                        CancellationToken.None);
                }
                finally
                {
                    Interlocked.Decrement(ref _activeTransmits);
                }
            }

            public ValueTask<HardwareOperationResult> FlushAsync(
                CancellationToken cancellationToken = default)
            {
                return _innerSession.FlushAsync(cancellationToken);
            }

            public ValueTask<HardwareOperationResult> StopAsync()
            {
                if (Volatile.Read(ref _activeTransmits) != 0)
                {
                    Interlocked.Exchange(ref _stopCalledBeforeTransmitCompleted, 1);
                }

                return _innerSession.StopAsync();
            }

            public ValueTask DisposeAsync()
            {
                return _innerSession.DisposeAsync();
            }

            public void ReleaseTransmit()
            {
                _releaseTransmit.TrySetResult();
            }
        }
    }
}
