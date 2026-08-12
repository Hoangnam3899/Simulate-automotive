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
        private readonly ICanGatewaySession _session;
        private readonly Dictionary<
            (uint CanIdentifier, bool IsExtendedIdentifier),
            SimulationMessageRule> _enabledRules;

        private CancellationTokenSource? _runCancellation;
        private Task? _workerTask;
        private Task? _stopTask;
        private bool _isDisposed;
        private long _receivedFrames;
        private long _transmittedFrames;
        private long _passedFrames;
        private long _droppedFrames;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimulationEngine"/> class.
        /// </summary>
        /// <param name="session">An open session whose lifetime remains owned by the caller.</param>
        /// <param name="plan">The immutable plan used for frame routing decisions.</param>
        public SimulationEngine(ICanGatewaySession session, SimulationPlan plan)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(plan);

            _session = session;
            _enabledRules = plan.MessageRules
                .Where(rule => rule.IsEnabled)
                .ToDictionary(
                    rule => (rule.CanIdentifier, rule.IsExtendedIdentifier));
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
            Interlocked.Read(ref _droppedFrames));

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
                HardwareOperationResult result = await _session
                    .TransmitAsync(destination, routedFrame.Frame, cancellationToken)
                    .ConfigureAwait(false);

                if (!result.IsSuccess)
                {
                    throw new HardwareOperationException(result.Failure!);
                }

                Interlocked.Increment(ref _passedFrames);
                Interlocked.Increment(ref _transmittedFrames);
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
        }
    }
}
