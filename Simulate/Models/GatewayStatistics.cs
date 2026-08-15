using System;

namespace Simulate.Models
{
    /// <summary>
    /// Represents an immutable snapshot of gateway counters and routing telemetry.
    /// </summary>
    public sealed class GatewayStatistics
    {
        /// <summary>
        /// Gets an immutable counter snapshot with every value set to zero.
        /// </summary>
        public static GatewayStatistics Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, null);

        internal GatewayStatistics(
            long receivedFrames,
            long transmittedFrames,
            long passedFrames,
            long droppedFrames,
            long injectedFrames,
            long filteredEchoFrames,
            long scheduledFrames,
            TimeSpan? lastRoutingLatency)
        {
            ReceivedFrames = receivedFrames;
            TransmittedFrames = transmittedFrames;
            PassedFrames = passedFrames;
            DroppedFrames = droppedFrames;
            InjectedFrames = injectedFrames;
            FilteredEchoFrames = filteredEchoFrames;
            ScheduledFrames = scheduledFrames;
            LastRoutingLatency = lastRoutingLatency;
        }

        /// <summary>
        /// Gets the number of frames consumed from the hardware session.
        /// </summary>
        public long ReceivedFrames { get; }

        /// <summary>
        /// Gets the number of frames accepted for transmission by the hardware session.
        /// </summary>
        public long TransmittedFrames { get; }

        /// <summary>
        /// Gets the number of frames forwarded without payload modification.
        /// </summary>
        public long PassedFrames { get; }

        /// <summary>
        /// Gets the number of frames suppressed by an enabled block rule.
        /// </summary>
        public long DroppedFrames { get; }

        /// <summary>
        /// Gets the number of frames forwarded after a signal override or E2E update.
        /// </summary>
        public long InjectedFrames { get; }

        /// <summary>
        /// Gets the number of matching loopback frames consumed inside the echo window.
        /// </summary>
        public long FilteredEchoFrames { get; }

        /// <summary>
        /// Gets the number of frames emitted by one-shot, cyclic, or event scheduling.
        /// </summary>
        public long ScheduledFrames { get; }

        /// <summary>
        /// Gets the latest successful software gateway-routing latency in this run.
        /// </summary>
        public TimeSpan? LastRoutingLatency { get; }
    }
}
