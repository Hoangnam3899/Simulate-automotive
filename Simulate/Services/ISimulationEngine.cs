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
        /// Gets the latest immutable gateway counter snapshot.
        /// </summary>
        GatewayStatistics Statistics { get; }

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
        /// Cancels the receive loop and waits for its background task to finish.
        /// </summary>
        /// <returns>A value task that completes after the receive loop has stopped.</returns>
        ValueTask StopAsync();
    }
}
