namespace Simulate.Models
{
    /// <summary>
    /// Represents an immutable snapshot of gateway frame counters.
    /// </summary>
    public sealed class GatewayStatistics
    {
        internal GatewayStatistics(
            long receivedFrames,
            long transmittedFrames,
            long passedFrames,
            long droppedFrames,
            long injectedFrames,
            long filteredEchoFrames)
        {
            ReceivedFrames = receivedFrames;
            TransmittedFrames = transmittedFrames;
            PassedFrames = passedFrames;
            DroppedFrames = droppedFrames;
            InjectedFrames = injectedFrames;
            FilteredEchoFrames = filteredEchoFrames;
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
    }
}
