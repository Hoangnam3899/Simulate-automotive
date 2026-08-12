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
        private readonly ICanGatewaySession _session;
        private readonly SimulationEngineOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly List<PendingEcho> _pendingEchoes = new();
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

        private CancellationTokenSource? _runCancellation;
        private Task? _workerTask;
        private Task? _stopTask;
        private bool _isDisposed;
        private long _receivedFrames;
        private long _transmittedFrames;
        private long _passedFrames;
        private long _droppedFrames;
        private long _injectedFrames;
        private long _filteredEchoFrames;

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
        /// <param name="options">The echo-filter window and capacity.</param>
        /// <param name="timeProvider">The monotonic time source used to expire echo entries.</param>
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
            Interlocked.Read(ref _filteredEchoFrames));

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
        public ValueTask StopAsync()
        {
            lock (_lifecycleSync)
            {
                if (_workerTask is null || _runCancellation is null)
                {
                    return ValueTask.CompletedTask;
                }

                _stopTask ??= StopCoreAsync(_workerTask, _runCancellation);
                return new ValueTask(_stopTask);
            }
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
            CancellationTokenSource runCancellation)
        {
            runCancellation.Cancel();

            try
            {
                await workerTask.ConfigureAwait(false);
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
    }
}
