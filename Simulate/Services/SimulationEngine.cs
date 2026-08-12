using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Simulate.Models;

namespace Simulate.Services
{
    /// <summary>
    /// Routes frames between both sides of a CAN gateway session according to a simulation plan.
    /// </summary>
    public sealed class SimulationEngine : ISimulationEngine
    {
        private readonly object _lifecycleSync = new();
        private readonly object _overrideSync = new();
        private readonly object _echoSync = new();
        private readonly object _eventSync = new();
        private readonly object _baselineSync = new();
        private readonly ICanGatewaySession _session;
        private readonly SimulationEngineOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly List<PendingEcho> _pendingEchoes = new();
        private readonly AsyncManualResetGate _scheduleGate = new(isSet: true);
        private readonly SemaphoreSlim _transmitGate = new(1, 1);
        private readonly Dictionary<
            (uint CanIdentifier, bool IsExtendedIdentifier),
            SimulationMessageRule> _enabledRules;
        private readonly Dictionary<
            (uint CanIdentifier, bool IsExtendedIdentifier),
            DbcMessage> _messages;
        private Dictionary<
            (uint CanIdentifier, bool IsExtendedIdentifier),
            SignalOverride[]> _overrideSnapshot;
        private readonly Dictionary<
            (uint CanIdentifier, bool IsExtendedIdentifier),
            int> _e2eCounters = new();
        private readonly Dictionary<
            (uint CanIdentifier, bool IsExtendedIdentifier),
            long> _lastEventTriggers = new();
        private readonly Dictionary<
            (uint CanIdentifier, bool IsExtendedIdentifier),
            CanFrame> _lastRxFrames = new();

        private CancellationTokenSource? _runCancellation;
        private CancellationTokenSource? _scheduleCancellation;
        private Task? _workerTask;
        private Task? _schedulerTask;
        private Task? _scheduleStopTask;
        private Task? _stopTask;
        private TaskCompletionSource _eventTriggersDrained = CreateCompletedSignal();
        private int _activeEventTriggers;
        private bool _isDisposed;
        private bool _isSchedulingPaused;
        private long _receivedFrames;
        private long _transmittedFrames;
        private long _passedFrames;
        private long _droppedFrames;
        private long _injectedFrames;
        private long _filteredEchoFrames;
        private long _scheduledFrames;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimulationEngine"/> class.
        /// </summary>
        /// <param name="session">An open session whose lifetime remains owned by the caller.</param>
        /// <param name="plan">The immutable plan used for frame routing decisions.</param>
        public SimulationEngine(ICanGatewaySession session, SimulationPlan plan)
            : this(session, plan, SimulationEngineOptions.Default, TimeProvider.System)
        {
        }

        /// <summary>
        /// Initializes an engine with explicit echo-filter safeguards and a monotonic time source.
        /// </summary>
        /// <param name="session">An open session whose lifetime remains owned by the caller.</param>
        /// <param name="plan">The immutable plan used for frame routing decisions.</param>
        /// <param name="options">The bounded echo-filter and event-debounce safeguards.</param>
        /// <param name="timeProvider">The monotonic time source used by echo filtering and scheduling.</param>
        public SimulationEngine(
            ICanGatewaySession session,
            SimulationPlan plan,
            SimulationEngineOptions options,
            TimeProvider timeProvider)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(timeProvider);

            _session = session;
            _options = options;
            _timeProvider = timeProvider;
            _enabledRules = plan.MessageRules
                .Where(rule => rule.IsEnabled)
                .ToDictionary(
                    rule => (rule.CanIdentifier, rule.IsExtendedIdentifier));
            _messages = plan.Document.Messages.ToDictionary(
                message => (message.Identifier, message.IsExtendedIdentifier));
            foreach (KeyValuePair<
                (uint CanIdentifier, bool IsExtendedIdentifier),
                SimulationMessageRule> entry in _enabledRules)
            {
                if (entry.Value.GatewayMode == GatewayMode.Inject)
                {
                    ValidateSignalOverrides(
                        _messages[entry.Key],
                        entry.Value.SignalOverrides);
                }
            }

            _overrideSnapshot = _enabledRules
                .Where(entry => entry.Value.GatewayMode == GatewayMode.Inject)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value.SignalOverrides.ToArray());
        }

        /// <inheritdoc />
        public bool IsRunning
        {
            get
            {
                lock (_lifecycleSync)
                {
                    return _workerTask is { IsCompleted: false };
                }
            }
        }

        /// <inheritdoc />
        public GatewayStatistics Statistics => new(
            Interlocked.Read(ref _receivedFrames),
            Interlocked.Read(ref _transmittedFrames),
            Interlocked.Read(ref _passedFrames),
            Interlocked.Read(ref _droppedFrames),
            Interlocked.Read(ref _injectedFrames),
            Interlocked.Read(ref _filteredEchoFrames),
            Interlocked.Read(ref _scheduledFrames));

        /// <inheritdoc />
        public void ReplaceSignalOverrides(
            uint canIdentifier,
            bool isExtendedIdentifier,
            IEnumerable<SignalOverride> signalOverrides)
        {
            ArgumentNullException.ThrowIfNull(signalOverrides);
            SignalOverride[] replacement = signalOverrides.ToArray();
            if (replacement.Any(signalOverride => signalOverride is null))
            {
                throw new ArgumentException(
                    "A runtime override set cannot contain a null value.",
                    nameof(signalOverrides));
            }

            bool hasDuplicateSignal = replacement
                .GroupBy(signalOverride => signalOverride.SignalName, StringComparer.Ordinal)
                .Any(group => group.Count() > 1);
            if (hasDuplicateSignal)
            {
                throw new ArgumentException(
                    "A runtime override set cannot contain duplicate signal names.",
                    nameof(signalOverrides));
            }

            var key = (CanIdentifier: canIdentifier, IsExtendedIdentifier: isExtendedIdentifier);
            if (!_enabledRules.TryGetValue(key, out SimulationMessageRule? rule)
                || rule.GatewayMode != GatewayMode.Inject)
            {
                throw new ArgumentException(
                    "Runtime signal overrides require a configured, enabled inject rule.",
                    nameof(canIdentifier));
            }

            DbcMessage message = _messages[key];
            ValidateSignalOverrides(message, replacement);

            lock (_overrideSync)
            {
                var nextSnapshot = new Dictionary<
                    (uint CanIdentifier, bool IsExtendedIdentifier),
                    SignalOverride[]>(Volatile.Read(ref _overrideSnapshot))
                {
                    [key] = replacement
                };
                Volatile.Write(ref _overrideSnapshot, nextSnapshot);
            }
        }

        /// <inheritdoc />
        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_lifecycleSync)
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);

                if (_workerTask is not null)
                {
                    throw new InvalidOperationException(
                        "The simulation engine must be stopped before it can be started again.");
                }

                if (!_session.IsOpen)
                {
                    throw new InvalidOperationException(
                        "The simulation engine requires an open CAN gateway session.");
                }

                ResetStatistics();
                _e2eCounters.Clear();
                lock (_echoSync)
                {
                    _pendingEchoes.Clear();
                }
                lock (_baselineSync)
                {
                    _lastRxFrames.Clear();
                }

                _runCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _workerTask = Task.Run(
                    () => RunReceiveLoopAsync(_runCancellation.Token),
                    CancellationToken.None);
                _stopTask = null;
            }

            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public bool IsScheduling
        {
            get
            {
                lock (_lifecycleSync)
                {
                    return _schedulerTask is { IsCompleted: false };
                }
            }
        }

        /// <inheritdoc />
        public bool IsSchedulingPaused
        {
            get
            {
                lock (_lifecycleSync)
                {
                    return _isSchedulingPaused
                        && _schedulerTask is { IsCompleted: false };
                }
            }
        }

        /// <inheritdoc />
        public ValueTask StartSchedulingAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_lifecycleSync)
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
                if (_workerTask is not { IsCompleted: false }
                    || _runCancellation is null
                    || _stopTask is not null)
                {
                    throw new InvalidOperationException(
                        "The simulation engine must be running before scheduling starts.");
                }

                if (_schedulerTask is { IsCompleted: false }
                    || _scheduleStopTask is { IsCompleted: false })
                {
                    throw new InvalidOperationException(
                        "Simulation scheduling is already running.");
                }

                ValidateScheduledBaselineCompatibility();
                lock (_eventSync)
                {
                    _lastEventTriggers.Clear();
                }

                _scheduleCancellation?.Dispose();
                _scheduleStopTask = null;
                _scheduleCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    _runCancellation.Token,
                    cancellationToken);
                _isSchedulingPaused = false;
                _scheduleGate.Set();
                _schedulerTask = Task.Run(
                    () => RunSchedulerAsync(_scheduleCancellation.Token),
                    CancellationToken.None);
            }

            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public void PauseScheduling()
        {
            lock (_lifecycleSync)
            {
                if (_schedulerTask is not { IsCompleted: false }
                    || _scheduleStopTask is { IsCompleted: false }
                    || _stopTask is not null)
                {
                    return;
                }

                _isSchedulingPaused = true;
                _scheduleGate.Reset();
            }
        }

        /// <inheritdoc />
        public void ResumeScheduling()
        {
            lock (_lifecycleSync)
            {
                if (_schedulerTask is not { IsCompleted: false }
                    || _scheduleStopTask is { IsCompleted: false }
                    || _stopTask is not null)
                {
                    return;
                }

                _isSchedulingPaused = false;
                _scheduleGate.Set();
            }
        }

        /// <inheritdoc />
        public ValueTask StopSchedulingAsync()
        {
            lock (_lifecycleSync)
            {
                if (_schedulerTask is null || _scheduleCancellation is null)
                {
                    return ValueTask.CompletedTask;
                }

                _scheduleStopTask ??= StopSchedulingCoreAsync(
                    _schedulerTask,
                    _scheduleCancellation,
                    _eventTriggersDrained.Task);
                return new ValueTask(_scheduleStopTask);
            }
        }

        /// <inheritdoc />
        public async ValueTask<SimulationEventTriggerResult> TriggerEventAsync(
            uint canIdentifier,
            bool isExtendedIdentifier,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = (CanIdentifier: canIdentifier, IsExtendedIdentifier: isExtendedIdentifier);
            if (!_enabledRules.TryGetValue(key, out SimulationMessageRule? rule)
                || rule.SendType != SimulationSendType.Event
                || rule.GatewayMode == GatewayMode.Block)
            {
                throw new ArgumentException(
                    "An event trigger requires an enabled, non-blocking event rule.",
                    nameof(canIdentifier));
            }

            CancellationToken scheduleToken;
            lock (_lifecycleSync)
            {
                if (_schedulerTask is not { IsCompleted: false }
                    || _scheduleCancellation is null
                    || _scheduleStopTask is { IsCompleted: false }
                    || _stopTask is not null)
                {
                    throw new InvalidOperationException(
                        "Simulation scheduling must be active before an event is triggered.");
                }

                scheduleToken = _scheduleCancellation.Token;
                if (_activeEventTriggers++ == 0)
                {
                    _eventTriggersDrained = new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }

            try
            {
                long now = _timeProvider.GetTimestamp();
                lock (_eventSync)
                {
                    if (_lastEventTriggers.TryGetValue(key, out long previousTrigger)
                        && _timeProvider.GetElapsedTime(previousTrigger, now) < _options.EventDebounce)
                    {
                        return SimulationEventTriggerResult.Debounced;
                    }

                    _lastEventTriggers[key] = now;
                }

                using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    scheduleToken,
                    cancellationToken);
                if (rule.Timing.StartDelay > TimeSpan.Zero)
                {
                    await Task.Delay(
                        rule.Timing.StartDelay,
                        _timeProvider,
                        linkedCancellation.Token).ConfigureAwait(false);
                }

                await TransmitScheduledFrameAsync(
                    rule,
                    linkedCancellation.Token).ConfigureAwait(false);
                return SimulationEventTriggerResult.Transmitted;
            }
            finally
            {
                CompleteEventTrigger();
            }
        }

        /// <inheritdoc />
        public ValueTask StopAsync()
        {
            lock (_lifecycleSync)
            {
                if (_workerTask is null || _runCancellation is null)
                {
                    return ValueTask.CompletedTask;
                }

                _stopTask ??= StopCoreAsync(
                    _workerTask,
                    _schedulerTask,
                    _eventTriggersDrained.Task,
                    _runCancellation);
                return new ValueTask(_stopTask);
            }
        }

        /// <inheritdoc />
        public async ValueTask<HardwareOperationResult> EmergencyStopAsync()
        {
            HardwareOperationResult sessionStopResult = null!;
            try
            {
                await StopAsync().ConfigureAwait(false);
            }
            finally
            {
                sessionStopResult = await _session.StopAsync().ConfigureAwait(false);
            }

            return sessionStopResult;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            lock (_lifecycleSync)
            {
                _isDisposed = true;
            }

            await StopAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        private async Task RunReceiveLoopAsync(CancellationToken cancellationToken)
        {
            await foreach (RoutedCanFrame routedFrame in
                _session.ReceiveAsync(cancellationToken).ConfigureAwait(false))
            {
                Interlocked.Increment(ref _receivedFrames);

                if (TryConsumeEcho(routedFrame))
                {
                    Interlocked.Increment(ref _filteredEchoFrames);
                    continue;
                }

                if (routedFrame.Source == CanGatewaySide.Rx)
                {
                    lock (_baselineSync)
                    {
                        _lastRxFrames[
                            (routedFrame.Frame.Identifier,
                                routedFrame.Frame.IsExtendedIdentifier)] = routedFrame.Frame;
                    }
                }

                if (_enabledRules.TryGetValue(
                        (routedFrame.Frame.Identifier, routedFrame.Frame.IsExtendedIdentifier),
                        out SimulationMessageRule? rule)
                    && rule.GatewayMode == GatewayMode.Block)
                {
                    Interlocked.Increment(ref _droppedFrames);
                    continue;
                }

                CanGatewaySide destination = routedFrame.Source switch
                {
                    CanGatewaySide.Rx => CanGatewaySide.Tx,
                    CanGatewaySide.Tx => CanGatewaySide.Rx,
                    _ => throw new InvalidOperationException("The CAN gateway side is not defined.")
                };
                await _transmitGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    bool wasModified = false;
                    CanFrame outboundFrame = rule?.GatewayMode == GatewayMode.Inject
                        ? CreateInjectedFrame(routedFrame.Frame, rule, out wasModified)
                        : routedFrame.Frame;
                    HardwareOperationResult result = await _session
                        .TransmitAsync(destination, outboundFrame, cancellationToken)
                        .ConfigureAwait(false);

                    if (!result.IsSuccess)
                    {
                        throw new HardwareOperationException(result.Failure!);
                    }

                    RegisterPendingEcho(destination, outboundFrame);

                    if (wasModified)
                    {
                        Interlocked.Increment(ref _injectedFrames);
                    }
                    else
                    {
                        Interlocked.Increment(ref _passedFrames);
                    }

                    Interlocked.Increment(ref _transmittedFrames);
                }
                finally
                {
                    _transmitGate.Release();
                }
            }
        }

        private async Task RunSchedulerAsync(CancellationToken cancellationToken)
        {
            IEnumerable<Task> scheduledTasks = _enabledRules.Values
                .Where(rule => rule.GatewayMode != GatewayMode.Block)
                .Where(rule => rule.SendType != SimulationSendType.Event)
                .Select(rule => rule.SendType switch
                {
                    SimulationSendType.OneShot => RunOneShotAsync(rule, cancellationToken),
                    SimulationSendType.Cyclic => RunCyclicAsync(rule, cancellationToken),
                    _ => throw new InvalidOperationException(
                        "The simulation send type is not supported by the scheduler.")
                });
            await Task.WhenAll(scheduledTasks).ConfigureAwait(false);
            if (_enabledRules.Values.Any(rule =>
                rule.GatewayMode != GatewayMode.Block
                && rule.SendType == SimulationSendType.Event))
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task RunOneShotAsync(
            SimulationMessageRule rule,
            CancellationToken cancellationToken)
        {
            if (rule.Timing.StartDelay > TimeSpan.Zero)
            {
                await Task.Delay(
                    rule.Timing.StartDelay,
                    _timeProvider,
                    cancellationToken).ConfigureAwait(false);
            }

            await TransmitScheduledFrameAsync(rule, cancellationToken).ConfigureAwait(false);
        }

        private async Task RunCyclicAsync(
            SimulationMessageRule rule,
            CancellationToken cancellationToken)
        {
            if (rule.Timing.StartDelay > TimeSpan.Zero)
            {
                await Task.Delay(
                    rule.Timing.StartDelay,
                    _timeProvider,
                    cancellationToken).ConfigureAwait(false);
            }

            int sentFrames = 0;
            while (rule.Timing.RepeatCount == 0 || sentFrames < rule.Timing.RepeatCount)
            {
                await TransmitScheduledFrameAsync(rule, cancellationToken).ConfigureAwait(false);
                sentFrames++;
                if (rule.Timing.RepeatCount != 0
                    && sentFrames >= rule.Timing.RepeatCount)
                {
                    break;
                }

                await Task.Delay(
                    rule.Timing.CycleInterval!.Value,
                    _timeProvider,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task TransmitScheduledFrameAsync(
            SimulationMessageRule rule,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                await _scheduleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                await _transmitGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CanFrame outboundFrame = null!;
                    bool wasModified = false;
                    ValueTask<HardwareOperationResult> transmission = default;
                    bool waitForResume;
                    lock (_lifecycleSync)
                    {
                        // Pause and dispatch share this lock so queued schedule work cannot
                        // cross the pause boundary after gateway traffic releases the TX gate.
                        waitForResume = _isSchedulingPaused;
                        if (!waitForResume)
                        {
                            DbcMessage message =
                                _messages[(rule.CanIdentifier, rule.IsExtendedIdentifier)];
                            CanFrame baseline = GetScheduledBaseline(message);
                            outboundFrame = rule.GatewayMode == GatewayMode.Inject
                                ? CreateInjectedFrame(baseline, rule, out wasModified)
                                : baseline;
                            transmission = _session.TransmitAsync(
                                CanGatewaySide.Tx,
                                outboundFrame,
                                cancellationToken);
                        }
                    }

                    if (waitForResume)
                    {
                        continue;
                    }

                    HardwareOperationResult result =
                        await transmission.ConfigureAwait(false);
                    if (!result.IsSuccess)
                    {
                        throw new HardwareOperationException(result.Failure!);
                    }

                    RegisterPendingEcho(CanGatewaySide.Tx, outboundFrame);
                    Interlocked.Increment(ref _scheduledFrames);
                    Interlocked.Increment(ref _transmittedFrames);
                    if (wasModified)
                    {
                        Interlocked.Increment(ref _injectedFrames);
                    }

                    return;
                }
                finally
                {
                    _transmitGate.Release();
                }
            }
        }

        private CanFrame CreateZeroBaselineFrame(DbcMessage message)
        {
            byte[] payload = new byte[message.PayloadLength];
            if (_session.Options.BusMode == CanBusMode.Classic)
            {
                return CanFrame.CreateClassic(
                    message.Identifier,
                    message.IsExtendedIdentifier,
                    payload);
            }

            return CanFrame.CreateFlexibleDataRate(
                message.Identifier,
                message.IsExtendedIdentifier,
                GetDataLengthCode(message.PayloadLength),
                isBitRateSwitchEnabled: true,
                payload);
        }

        private void ValidateScheduledBaselineCompatibility()
        {
            foreach (SimulationMessageRule rule in _enabledRules.Values.Where(rule =>
                rule.GatewayMode != GatewayMode.Block))
            {
                DbcMessage message = _messages[
                    (rule.CanIdentifier, rule.IsExtendedIdentifier)];
                if (_session.Options.BusMode == CanBusMode.Classic
                    && message.PayloadLength > CanFrame.MaximumClassicPayloadLength)
                {
                    throw new InvalidOperationException(
                        "Classic CAN scheduling requires a DBC payload length from 0 to 8 bytes.");
                }

                if (_session.Options.BusMode == CanBusMode.FlexibleDataRate)
                {
                    _ = GetDataLengthCode(message.PayloadLength);
                }
            }
        }

        private static CanDataLengthCode GetDataLengthCode(int payloadLength)
        {
            return payloadLength switch
            {
                >= 0 and <= 8 => (CanDataLengthCode)payloadLength,
                12 => CanDataLengthCode.Bytes12,
                16 => CanDataLengthCode.Bytes16,
                20 => CanDataLengthCode.Bytes20,
                24 => CanDataLengthCode.Bytes24,
                32 => CanDataLengthCode.Bytes32,
                48 => CanDataLengthCode.Bytes48,
                64 => CanDataLengthCode.Bytes64,
                _ => throw new InvalidOperationException(
                    "CAN FD scheduling requires a DBC payload length that maps to a valid DLC.")
            };
        }

        private CanFrame GetScheduledBaseline(DbcMessage message)
        {
            lock (_baselineSync)
            {
                if (_lastRxFrames.TryGetValue(
                    (message.Identifier, message.IsExtendedIdentifier),
                    out CanFrame? baseline))
                {
                    return baseline;
                }
            }

            return CreateZeroBaselineFrame(message);
        }

        private bool TryConsumeEcho(RoutedCanFrame routedFrame)
        {
            long now = _timeProvider.GetTimestamp();
            lock (_echoSync)
            {
                RemoveExpiredEchoes(now);
                int matchIndex = _pendingEchoes.FindIndex(pendingEcho =>
                    pendingEcho.ExpectedSource == routedFrame.Source
                    && FramesAreEqual(pendingEcho.Frame, routedFrame.Frame));
                if (matchIndex < 0)
                {
                    return false;
                }

                _pendingEchoes.RemoveAt(matchIndex);
                return true;
            }
        }

        private void RegisterPendingEcho(CanGatewaySide expectedSource, CanFrame frame)
        {
            long now = _timeProvider.GetTimestamp();
            lock (_echoSync)
            {
                RemoveExpiredEchoes(now);
                if (_pendingEchoes.Count == _options.MaximumPendingEchoes)
                {
                    _pendingEchoes.RemoveAt(0);
                }

                _pendingEchoes.Add(new PendingEcho(expectedSource, frame, now));
            }
        }

        private void RemoveExpiredEchoes(long now)
        {
            _pendingEchoes.RemoveAll(pendingEcho =>
                _timeProvider.GetElapsedTime(pendingEcho.Timestamp, now) > _options.EchoWindow);
        }

        private static bool FramesAreEqual(CanFrame left, CanFrame right)
        {
            return left.Identifier == right.Identifier
                && left.IsExtendedIdentifier == right.IsExtendedIdentifier
                && left.Format == right.Format
                && left.IsBitRateSwitchEnabled == right.IsBitRateSwitchEnabled
                && left.DataLengthCode == right.DataLengthCode
                && left.Data.Span.SequenceEqual(right.Data.Span);
        }

        private CanFrame CreateInjectedFrame(
            CanFrame liveFrame,
            SimulationMessageRule rule,
            out bool wasModified)
        {
            var key = (
                CanIdentifier: rule.CanIdentifier,
                IsExtendedIdentifier: rule.IsExtendedIdentifier);
            DbcMessage message = _messages[key];
            byte[] payload = liveFrame.Data.ToArray();
            Dictionary<
                (uint CanIdentifier, bool IsExtendedIdentifier),
                SignalOverride[]> overrideSnapshot = Volatile.Read(ref _overrideSnapshot);
            overrideSnapshot.TryGetValue(key, out SignalOverride[]? signalOverrides);

            foreach (SignalOverride signalOverride in signalOverrides ?? [])
            {
                DbcSignal signal = message.Signals.First(candidate =>
                    string.Equals(
                        candidate.Name,
                        signalOverride.SignalName,
                        StringComparison.Ordinal));
                SignalCodec.PackPhysical(payload, signal, signalOverride.PhysicalValue);
            }

            int previousCounter = _e2eCounters.TryGetValue(key, out int currentCounter)
                ? currentCounter
                : -1;
            E2eProtectionResult e2eResult = E2eProtector.Apply(
                payload,
                rule.E2eProtection,
                previousCounter);
            if (e2eResult.IsApplied)
            {
                _e2eCounters[key] = e2eResult.Counter;
            }

            wasModified = (signalOverrides?.Length ?? 0) > 0 || e2eResult.IsApplied;

            return liveFrame.Format == CanFrameFormat.Classic
                ? CanFrame.CreateClassic(
                    liveFrame.Identifier,
                    liveFrame.IsExtendedIdentifier,
                    payload)
                : CanFrame.CreateFlexibleDataRate(
                    liveFrame.Identifier,
                    liveFrame.IsExtendedIdentifier,
                    liveFrame.DataLengthCode,
                    liveFrame.IsBitRateSwitchEnabled,
                    payload);
        }

        private static void ValidateSignalOverrides(
            DbcMessage message,
            IEnumerable<SignalOverride> signalOverrides)
        {
            byte[] validationPayload = new byte[message.PayloadLength];
            foreach (SignalOverride signalOverride in signalOverrides)
            {
                DbcSignal? signal = message.Signals.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Name,
                        signalOverride.SignalName,
                        StringComparison.Ordinal));
                if (signal is null)
                {
                    throw new ArgumentException(
                        "Every runtime override must reference a signal in the configured DBC message.",
                        nameof(signalOverrides));
                }

                SignalCodec.PackPhysical(
                    validationPayload,
                    signal,
                    signalOverride.PhysicalValue);
            }
        }

        private async Task StopCoreAsync(
            Task workerTask,
            Task? schedulerTask,
            Task eventTriggersDrained,
            CancellationTokenSource runCancellation)
        {
            runCancellation.Cancel();

            try
            {
                await Task.WhenAll(
                    workerTask,
                    schedulerTask ?? Task.CompletedTask,
                    eventTriggersDrained).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
            {
            }
            finally
            {
                lock (_lifecycleSync)
                {
                    if (ReferenceEquals(_workerTask, workerTask))
                    {
                        _workerTask = null;
                        _runCancellation = null;
                        _schedulerTask = null;
                        _scheduleStopTask = null;
                        _isSchedulingPaused = false;
                        _scheduleGate.Set();
                        _scheduleCancellation?.Dispose();
                        _scheduleCancellation = null;
                    }
                }

                runCancellation.Dispose();
            }
        }

        private void ResetStatistics()
        {
            Interlocked.Exchange(ref _receivedFrames, 0);
            Interlocked.Exchange(ref _transmittedFrames, 0);
            Interlocked.Exchange(ref _passedFrames, 0);
            Interlocked.Exchange(ref _droppedFrames, 0);
            Interlocked.Exchange(ref _injectedFrames, 0);
            Interlocked.Exchange(ref _filteredEchoFrames, 0);
            Interlocked.Exchange(ref _scheduledFrames, 0);
        }

        private sealed class PendingEcho
        {
            public PendingEcho(
                CanGatewaySide expectedSource,
                CanFrame frame,
                long timestamp)
            {
                ExpectedSource = expectedSource;
                Frame = frame;
                Timestamp = timestamp;
            }

            public CanGatewaySide ExpectedSource { get; }

            public CanFrame Frame { get; }

            public long Timestamp { get; }
        }

        private async Task StopSchedulingCoreAsync(
            Task schedulerTask,
            CancellationTokenSource scheduleCancellation,
            Task eventTriggersDrained)
        {
            scheduleCancellation.Cancel();
            _scheduleGate.Set();
            try
            {
                await Task.WhenAll(
                    schedulerTask,
                    eventTriggersDrained).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (scheduleCancellation.IsCancellationRequested)
            {
            }
            finally
            {
                lock (_lifecycleSync)
                {
                    if (ReferenceEquals(_schedulerTask, schedulerTask))
                    {
                        _schedulerTask = null;
                        _scheduleCancellation = null;
                        _scheduleStopTask = null;
                        _isSchedulingPaused = false;
                    }
                }

                scheduleCancellation.Dispose();
            }
        }

        private void CompleteEventTrigger()
        {
            lock (_lifecycleSync)
            {
                _activeEventTriggers--;
                if (_activeEventTriggers == 0)
                {
                    _eventTriggersDrained.TrySetResult();
                }
            }
        }

        private static TaskCompletionSource CreateCompletedSignal()
        {
            var completionSource = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            completionSource.TrySetResult();
            return completionSource;
        }

        private sealed class AsyncManualResetGate
        {
            private readonly object _sync = new();
            private TaskCompletionSource _completionSource;

            public AsyncManualResetGate(bool isSet)
            {
                _completionSource = CreateCompletionSource();
                if (isSet)
                {
                    _completionSource.TrySetResult();
                }
            }

            public Task WaitAsync(CancellationToken cancellationToken)
            {
                Task task;
                lock (_sync)
                {
                    task = _completionSource.Task;
                }

                return task.WaitAsync(cancellationToken);
            }

            public void Reset()
            {
                lock (_sync)
                {
                    if (_completionSource.Task.IsCompleted)
                    {
                        _completionSource = CreateCompletionSource();
                    }
                }
            }

            public void Set()
            {
                lock (_sync)
                {
                    _completionSource.TrySetResult();
                }
            }

            private static TaskCompletionSource CreateCompletionSource()
            {
                return new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }
}
