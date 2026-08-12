using System;

namespace Simulate.Models
{
    /// <summary>
    /// Defines bounded runtime safeguards used by the simulation engine.
    /// </summary>
    public sealed class SimulationEngineOptions
    {
        /// <summary>
        /// Gets the reference-compatible echo window and cache capacity.
        /// </summary>
        public static SimulationEngineOptions Default { get; } = new(
            TimeSpan.FromMilliseconds(10),
            maximumPendingEchoes: 32);

        /// <summary>
        /// Initializes a new instance of the <see cref="SimulationEngineOptions"/> class.
        /// </summary>
        /// <param name="echoWindow">The maximum age of a frame eligible for echo filtering.</param>
        /// <param name="maximumPendingEchoes">The hard upper bound for unmatched transmitted frames.</param>
        public SimulationEngineOptions(
            TimeSpan echoWindow,
            int maximumPendingEchoes)
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

            EchoWindow = echoWindow;
            MaximumPendingEchoes = maximumPendingEchoes;
        }

        /// <summary>
        /// Gets the maximum age of a frame eligible for echo filtering.
        /// </summary>
        public TimeSpan EchoWindow { get; }

        /// <summary>
        /// Gets the maximum number of unmatched transmissions retained for echo filtering.
        /// </summary>
        public int MaximumPendingEchoes { get; }
    }
}
