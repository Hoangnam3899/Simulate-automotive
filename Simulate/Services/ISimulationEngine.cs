using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simulate.Models;

namespace Simulate.Services
{
    /// <summary>
    /// Runs a simulation plan over an already-open CAN gateway session.
    /// </summary>
    public interface ISimulationEngine : IAsyncDisposable
    {
        /// <summary>
        /// Gets a value indicating whether the receive loop is still active.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets a value indicating whether one-shot, cyclic, or event scheduling is active.
        /// </summary>
        bool IsScheduling { get; }

        /// <summary>
        /// Gets a value indicating whether scheduled sends are paused while gateway routing continues.
        /// </summary>
        bool IsSchedulingPaused { get; }

        /// <summary>
        /// Gets the latest immutable gateway counter snapshot.
        /// </summary>
        GatewayStatistics Statistics { get; }

        /// <summary>
        /// Gets the first typed hardware failure captured during the current or most recent run.
        /// </summary>
        HardwareFailure? LastFailure { get; }

        /// <summary>
        /// Atomically replaces every active signal override for one configured inject rule.
        /// </summary>
        /// <param name="canIdentifier">The normalized CAN identifier from the DBC document.</param>
        /// <param name="isExtendedIdentifier">Whether the identifier uses the extended CAN format.</param>
        /// <param name="signalOverrides">The complete replacement set; an empty set clears all overrides.</param>
        void ReplaceSignalOverrides(
            uint canIdentifier,
            bool isExtendedIdentifier,
            IEnumerable<SignalOverride> signalOverrides);

        /// <summary>
        /// Starts processing frames until cancellation or an explicit stop.
        /// </summary>
        /// <param name="cancellationToken">Cancels the active receive loop.</param>
        /// <returns>A value task that completes after the loop has been started.</returns>
        ValueTask StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts one-shot/cyclic work and enables event triggers without changing gateway routing.
        /// Scheduled frames are emitted toward the configured TX gateway side.
        /// </summary>
        /// <param name="cancellationToken">Cancels only the active scheduling run.</param>
        /// <returns>A value task that completes after scheduling has started.</returns>
        ValueTask StartSchedulingAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Pauses scheduled sends without pausing or closing the gateway session.
        /// </summary>
        void PauseScheduling();

        /// <summary>
        /// Resumes scheduled sends; a send that became due while paused proceeds once.
        /// </summary>
        void ResumeScheduling();

        /// <summary>
        /// Cancels scheduled work while leaving gateway routing and the session active.
        /// </summary>
        /// <returns>A value task that completes after all scheduled sends have stopped.</returns>
        ValueTask StopSchedulingAsync();

        /// <summary>
        /// Requests one send for an enabled event-driven rule.
        /// </summary>
        /// <param name="canIdentifier">The normalized identifier of the event rule.</param>
        /// <param name="isExtendedIdentifier">Whether the event rule uses an extended identifier.</param>
        /// <param name="cancellationToken">Cancels this trigger while it is waiting for its start delay.</param>
        /// <returns>A typed transmitted or debounced outcome.</returns>
        ValueTask<SimulationEventTriggerResult> TriggerEventAsync(
            uint canIdentifier,
            bool isExtendedIdentifier,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels receive and scheduled work while leaving the caller-owned session open.
        /// </summary>
        /// <returns>A value task that completes after the receive loop has stopped.</returns>
        ValueTask StopAsync();

        /// <summary>
        /// Cancels receive and scheduling work before requesting cleanup of the caller-supplied session.
        /// </summary>
        /// <returns>The typed result returned by session cleanup.</returns>
        ValueTask<HardwareOperationResult> EmergencyStopAsync();
    }
}
