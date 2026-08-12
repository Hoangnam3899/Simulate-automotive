using System;

namespace Simulate.Models
{
    /// <summary>
    /// Defines bounded runtime safeguards used by the simulation engine.
    /// </summary>
    public sealed class SimulationEngineOptions
    {
        /// <summary>
        /// Gets the reference-compatible echo and event scheduling safeguards.
        /// </summary>
        public static SimulationEngineOptions Default { get; } = new(
            TimeSpan.FromMilliseconds(10),
            maximumPendingEchoes: 32,
            eventDebounce: TimeSpan.FromMilliseconds(50));

        /// <summary>
        /// Initializes a new instance of the <see cref="SimulationEngineOptions"/> class.
        /// </summary>
        /// <param name="echoWindow">The maximum age of a frame eligible for echo filtering.</param>
        /// <param name="maximumPendingEchoes">The hard upper bound for unmatched transmitted frames.</param>
        public SimulationEngineOptions(
            TimeSpan echoWindow,
            int maximumPendingEchoes)
            : this(
                echoWindow,
                maximumPendingEchoes,
                eventDebounce: TimeSpan.FromMilliseconds(50))
        {
        }

        /// <summary>
        /// Initializes runtime safeguards for echo filtering and event scheduling.
        /// </summary>
        /// <param name="echoWindow">The maximum age of a frame eligible for echo filtering.</param>
        /// <param name="maximumPendingEchoes">The hard upper bound for unmatched transmitted frames.</param>
        /// <param name="eventDebounce">The minimum elapsed time between accepted event triggers.</param>
        public SimulationEngineOptions(
            TimeSpan echoWindow,
            int maximumPendingEchoes,
            TimeSpan eventDebounce)
        {
            if (echoWindow <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(echoWindow),
                    echoWindow,
                    "The echo window must be positive.");
            }

            if (maximumPendingEchoes <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumPendingEchoes),
                    maximumPendingEchoes,
                    "The pending echo capacity must be positive.");
            }

            if (eventDebounce < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(eventDebounce),
                    eventDebounce,
                    "The event debounce duration cannot be negative.");
            }

            EchoWindow = echoWindow;
            MaximumPendingEchoes = maximumPendingEchoes;
            EventDebounce = eventDebounce;
        }

        /// <summary>
        /// Gets the maximum age of a frame eligible for echo filtering.
        /// </summary>
        public TimeSpan EchoWindow { get; }

        /// <summary>
        /// Gets the maximum number of unmatched transmissions retained for echo filtering.
        /// </summary>
        public int MaximumPendingEchoes { get; }

        /// <summary>
        /// Gets the minimum elapsed time between accepted triggers for one event rule.
        /// </summary>
        public TimeSpan EventDebounce { get; }
    }
}
