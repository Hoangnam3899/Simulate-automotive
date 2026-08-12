namespace Simulate.Models
{
    /// <summary>
    /// Describes the observable outcome of one event-driven scheduling request.
    /// </summary>
    public enum SimulationEventTriggerResult
    {
        /// <summary>
        /// The configured frame was accepted for transmission.
        /// </summary>
        Transmitted,

        /// <summary>
        /// The request was suppressed inside the configured debounce window.
        /// </summary>
        Debounced
    }
}
