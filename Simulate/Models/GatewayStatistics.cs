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
            long droppedFrames)
        {
            ReceivedFrames = receivedFrames;
            TransmittedFrames = transmittedFrames;
            PassedFrames = passedFrames;
            DroppedFrames = droppedFrames;
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
    }
}
