using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.ViewModels
{
    /// <summary>
    /// Projects one immutable simulation plan and its caller-owned runtime engine into typed binding data.
    /// </summary>
    public partial class SimulationViewModel : ObservableObject
    {
        private readonly ISimulationEngine? _engine;

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private bool _isScheduling;

        [ObservableProperty]
        private bool _isSchedulingPaused;

        [ObservableProperty]
        private GatewayStatistics _statistics = GatewayStatistics.Empty;

        [ObservableProperty]
        private HardwareFailure? _lastFailure;

        /// <summary>
        /// Initializes an unconfigured projection for the UI before a DBC document and engine are composed.
        /// </summary>
        public SimulationViewModel()
        {
        }

        /// <summary>
        /// Initializes a projection from a validated plan and an already-created simulation engine.
        /// </summary>
        /// <param name="plan">The immutable DBC-bound simulation configuration to project.</param>
        /// <param name="engine">The caller-owned engine whose state is reflected without hardware access.</param>
        public SimulationViewModel(SimulationPlan plan, ISimulationEngine engine)
        {
            ArgumentNullException.ThrowIfNull(plan);
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));

            ProjectPlan(plan);
            RefreshRuntimeState();
        }

        /// <summary>
        /// Gets the DBC message rows for the existing message-list binding.
        /// </summary>
        public ObservableCollection<MessageModel> Messages { get; } = new();

        /// <summary>
        /// Gets the DBC signal rows for the existing signal-list bindings.
        /// </summary>
        public ObservableCollection<SignalModel> Signals { get; } = new();

        /// <summary>
        /// Gets the configured signal overrides projected as fault-queue rows.
        /// </summary>
        public ObservableCollection<FaultQueueModel> FaultQueue { get; } = new();

        /// <summary>
        /// Gets a value indicating whether this instance has an engine supplied at the composition boundary.
        /// </summary>
        public bool IsConfigured => _engine is not null;

        /// <summary>
        /// Refreshes observable runtime state without starting, stopping, or owning the engine.
        /// </summary>
        public void RefreshRuntimeState()
        {
            if (_engine is null)
            {
                IsRunning = false;
                IsScheduling = false;
                IsSchedulingPaused = false;
                Statistics = GatewayStatistics.Empty;
                LastFailure = null;
                return;
            }

            IsRunning = _engine.IsRunning;
            IsScheduling = _engine.IsScheduling;
            IsSchedulingPaused = _engine.IsSchedulingPaused;
            Statistics = _engine.Statistics;
            LastFailure = _engine.LastFailure;
        }

        /// <summary>
        /// Explicitly starts gateway routing through the configured engine.
        /// </summary>
        /// <param name="cancellationToken">Cancels the start operation before engine work begins.</param>
        /// <returns>A value task that completes after the engine accepted the start request.</returns>
        public async ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            await GetRequiredEngine().StartAsync(cancellationToken);
            RefreshRuntimeState();
        }

        /// <summary>
        /// Explicitly starts one-shot and cyclic scheduling through the configured engine.
        /// </summary>
        /// <param name="cancellationToken">Cancels the scheduling start request before engine work begins.</param>
        /// <returns>A value task that completes after scheduling has started.</returns>
        public async ValueTask StartSchedulingAsync(CancellationToken cancellationToken = default)
        {
            await GetRequiredEngine().StartSchedulingAsync(cancellationToken);
            RefreshRuntimeState();
        }

        /// <summary>
        /// Explicitly pauses scheduled sends while preserving gateway routing.
        /// </summary>
        public void PauseScheduling()
        {
            GetRequiredEngine().PauseScheduling();
            RefreshRuntimeState();
        }

        /// <summary>
        /// Explicitly resumes scheduled sends while preserving gateway routing.
        /// </summary>
        public void ResumeScheduling()
        {
            GetRequiredEngine().ResumeScheduling();
            RefreshRuntimeState();
        }

        /// <summary>
        /// Explicitly stops scheduled work while preserving gateway routing.
        /// </summary>
        /// <returns>A value task that completes after scheduled work has stopped.</returns>
        public async ValueTask StopSchedulingAsync()
        {
            try
            {
                await GetRequiredEngine().StopSchedulingAsync();
            }
            finally
            {
                RefreshRuntimeState();
            }
        }

        /// <summary>
        /// Explicitly requests an event-driven send and refreshes runtime state after it completes.
        /// </summary>
        /// <param name="canIdentifier">The normalized identifier of the configured event rule.</param>
        /// <param name="isExtendedIdentifier">Whether the configured event rule uses an extended identifier.</param>
        /// <param name="cancellationToken">Cancels the event trigger while it waits in the engine.</param>
        /// <returns>The typed engine outcome for the event trigger.</returns>
        public async ValueTask<SimulationEventTriggerResult> TriggerEventAsync(
            uint canIdentifier,
            bool isExtendedIdentifier,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await GetRequiredEngine().TriggerEventAsync(
                    canIdentifier,
                    isExtendedIdentifier,
                    cancellationToken);
            }
            finally
            {
                RefreshRuntimeState();
            }
        }

        /// <summary>
        /// Explicitly stops gateway routing and scheduled work through the configured engine.
        /// </summary>
        /// <returns>A value task that completes after engine work has stopped.</returns>
        public async ValueTask StopAsync()
        {
            try
            {
                await GetRequiredEngine().StopAsync();
            }
            finally
            {
                RefreshRuntimeState();
            }
        }

        /// <summary>
        /// Explicitly requests the engine's emergency stop while leaving session ownership with its caller.
        /// </summary>
        /// <returns>The typed cleanup result returned by the engine.</returns>
        public async ValueTask<HardwareOperationResult> EmergencyStopAsync()
        {
            try
            {
                return await GetRequiredEngine().EmergencyStopAsync();
            }
            finally
            {
                RefreshRuntimeState();
            }
        }

        private void ProjectPlan(SimulationPlan plan)
        {
            var rulesByMessage = plan.MessageRules.ToDictionary(
                rule => (rule.CanIdentifier, rule.IsExtendedIdentifier));
            var overridesBySignal = plan.MessageRules
                .SelectMany(rule => rule.SignalOverrides.Select(signalOverride => new
                {
                    rule.CanIdentifier,
                    rule.IsExtendedIdentifier,
                    signalOverride.SignalName,
                    signalOverride.PhysicalValue
                }))
                .ToDictionary(
                    item => (item.CanIdentifier, item.IsExtendedIdentifier, item.SignalName),
                    item => item.PhysicalValue);

            foreach (DbcMessage message in plan.Document.Messages)
            {
                rulesByMessage.TryGetValue(
                    (message.Identifier, message.IsExtendedIdentifier),
                    out SimulationMessageRule? rule);
                string messageId = FormatIdentifier(message.Identifier, message.IsExtendedIdentifier);
                Messages.Add(new MessageModel
                {
                    Id = messageId,
                    Name = message.Name,
                    Dlc = message.PayloadLength,
                    Cycle = FormatCycle(rule),
                    GatewayMode = rule?.GatewayMode.ToString() ?? "Unconfigured",
                    SendType = rule?.SendType.ToString() ?? "Unconfigured",
                    SignalCount = message.Signals.Count,
                    IsEnabled = rule?.IsEnabled ?? false
                });

                foreach (DbcSignal signal in message.Signals)
                {
                    bool isOverridden = overridesBySignal.TryGetValue(
                        (message.Identifier, message.IsExtendedIdentifier, signal.Name),
                        out double overrideValue);
                    Signals.Add(new SignalModel
                    {
                        Name = signal.Name,
                        StartBit = signal.StartBit,
                        Length = signal.BitLength,
                        Factor = signal.Factor,
                        Offset = signal.Offset,
                        Unit = signal.Unit,
                        Min = signal.Minimum,
                        Max = signal.Maximum,
                        Value = isOverridden ? overrideValue : signal.Offset,
                        MessageId = messageId,
                        MessageName = message.Name,
                        IsOverridden = isOverridden,
                        Cycle = FormatCycle(rule)
                    });
                }

                if (rule is not null)
                {
                    AddFaultQueueRows(message, messageId, rule);
                }
            }
        }

        private void AddFaultQueueRows(
            DbcMessage message,
            string messageId,
            SimulationMessageRule rule)
        {
            foreach (SignalOverride signalOverride in rule.SignalOverrides)
            {
                DbcSignal signal = message.Signals.Single(candidate =>
                    string.Equals(candidate.Name, signalOverride.SignalName, StringComparison.Ordinal));
                FaultQueue.Add(new FaultQueueModel
                {
                    Index = FaultQueue.Count + 1,
                    MsgId = messageId,
                    MsgName = message.Name,
                    Signal = signal.Name,
                    FaultType = "Signal override",
                    FaultValue = FormatPhysicalValue(signalOverride.PhysicalValue, signal.Unit),
                    StartTime = FormatDuration(rule.Timing.StartDelay),
                    Duration = "—",
                    Repeat = FormatRepeat(rule),
                    Mode = rule.GatewayMode.ToString(),
                    Status = rule.IsEnabled ? "Configured" : "Disabled",
                    StatusColor = rule.IsEnabled ? "#10B981" : "#94A3B8",
                    StatusBg = rule.IsEnabled ? "#064E3B" : "#1E2C3A"
                });
            }
        }

        private ISimulationEngine GetRequiredEngine()
        {
            return _engine ?? throw new InvalidOperationException(
                "A simulation plan and engine must be composed before runtime operations are requested.");
        }

        private static string FormatIdentifier(uint identifier, bool isExtendedIdentifier)
        {
            return isExtendedIdentifier
                ? $"0x{identifier:X8}"
                : $"0x{identifier:X3}";
        }

        private static string FormatCycle(SimulationMessageRule? rule)
        {
            if (rule is null)
            {
                return "—";
            }

            return rule.SendType switch
            {
                SimulationSendType.Cyclic => FormatDuration(rule.Timing.CycleInterval!.Value),
                SimulationSendType.OneShot => "One-shot",
                SimulationSendType.Event => "Event",
                _ => throw new InvalidOperationException("The simulation send type is not supported.")
            };
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalMilliseconds == 0d)
            {
                return "0 s";
            }

            if (duration.TotalMilliseconds < 1000d)
            {
                return duration.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) + " ms";
            }

            return duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " s";
        }

        private static string FormatPhysicalValue(double value, string unit)
        {
            string formattedValue = value.ToString("0.###", CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(unit) ? formattedValue : $"{formattedValue} {unit}";
        }

        private static string FormatRepeat(SimulationMessageRule rule)
        {
            return rule.SendType switch
            {
                SimulationSendType.Cyclic when rule.Timing.RepeatCount == 0 => "Infinite",
                SimulationSendType.Cyclic => rule.Timing.RepeatCount.ToString(CultureInfo.InvariantCulture),
                SimulationSendType.OneShot => "1",
                SimulationSendType.Event => "Event",
                _ => throw new InvalidOperationException("The simulation send type is not supported.")
            };
        }
    }
}
