using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Simulate.Models
{
    /// <summary>
    /// Cho biết cách gateway xử lý một CAN message được cấu hình.
    /// </summary>
    public enum GatewayMode
    {
        /// <summary>
        /// Chuyển tiếp payload gốc mà không thay đổi.
        /// </summary>
        PassThrough,

        /// <summary>
        /// Không phát message sang phía đối diện.
        /// </summary>
        Block,

        /// <summary>
        /// Áp dụng signal override trước khi phát message.
        /// </summary>
        Inject
    }

    /// <summary>
    /// Cho biết điều kiện phát của một message injection.
    /// </summary>
    public enum SimulationSendType
    {
        /// <summary>
        /// Phát đúng một lần sau start delay.
        /// </summary>
        OneShot,

        /// <summary>
        /// Phát lặp với cycle interval đã cấu hình.
        /// </summary>
        Cyclic,

        /// <summary>
        /// Phát khi event runtime phù hợp xảy ra.
        /// </summary>
        Event
    }

    /// <summary>
    /// Một giá trị physical thay thế cho signal có tên trong DBC message.
    /// </summary>
    public sealed class SignalOverride
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SignalOverride"/> class.
        /// </summary>
        public SignalOverride(string signalName, double physicalValue)
        {
            if (string.IsNullOrWhiteSpace(signalName))
            {
                throw new ArgumentException("The signal name is required.", nameof(signalName));
            }

            if (!double.IsFinite(physicalValue))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(physicalValue),
                    physicalValue,
                    "The signal override value must be finite.");
            }

            SignalName = signalName;
            PhysicalValue = physicalValue;
        }

        /// <summary>
        /// Gets the DBC signal name.
        /// </summary>
        public string SignalName { get; }

        /// <summary>
        /// Gets the physical value to encode into the signal.
        /// </summary>
        public double PhysicalValue { get; }
    }

    /// <summary>
    /// Thời điểm bắt đầu và nhịp lặp được khai báo cho message rule.
    /// </summary>
    public sealed class SimulationTiming
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SimulationTiming"/> class.
        /// </summary>
        public SimulationTiming(TimeSpan startDelay, TimeSpan? cycleInterval, int repeatCount)
        {
            if (startDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startDelay),
                    startDelay,
                    "The start delay cannot be negative.");
            }

            if (cycleInterval is not null && cycleInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cycleInterval),
                    cycleInterval,
                    "The cycle interval must be positive when configured.");
            }

            ArgumentOutOfRangeException.ThrowIfNegative(repeatCount);

            StartDelay = startDelay;
            CycleInterval = cycleInterval;
            RepeatCount = repeatCount;
        }

        /// <summary>
        /// Gets the delay before a scheduled send starts.
        /// </summary>
        public TimeSpan StartDelay { get; }

        /// <summary>
        /// Gets the interval between cyclic sends, when one is configured.
        /// </summary>
        public TimeSpan? CycleInterval { get; }

        /// <summary>
        /// Gets the number of cyclic sends; zero represents an unbounded sequence.
        /// </summary>
        public int RepeatCount { get; }
    }

    /// <summary>
    /// Cấu hình gateway và payload behavior cho đúng một DBC message.
    /// </summary>
    public sealed class SimulationMessageRule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SimulationMessageRule"/> class.
        /// </summary>
        public SimulationMessageRule(
            uint canIdentifier,
            bool isExtendedIdentifier,
            bool isEnabled,
            GatewayMode gatewayMode,
            SimulationSendType sendType,
            SimulationTiming timing,
            IEnumerable<SignalOverride> signalOverrides,
            E2eProtectionConfiguration e2eProtection)
        {
            ArgumentNullException.ThrowIfNull(timing);
            ArgumentNullException.ThrowIfNull(signalOverrides);
            ArgumentNullException.ThrowIfNull(e2eProtection);

            if (!Enum.IsDefined(gatewayMode))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gatewayMode),
                    gatewayMode,
                    "The gateway mode is not defined.");
            }

            if (!Enum.IsDefined(sendType))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sendType),
                    sendType,
                    "The simulation send type is not defined.");
            }

            if (sendType == SimulationSendType.Cyclic && timing.CycleInterval is null)
            {
                throw new ArgumentException(
                    "A cyclic send rule must declare a cycle interval.",
                    nameof(timing));
            }

            if (sendType != SimulationSendType.Cyclic && timing.CycleInterval is not null)
            {
                throw new ArgumentException(
                    "Only a cyclic send rule can declare a cycle interval.",
                    nameof(timing));
            }

            if (sendType == SimulationSendType.OneShot && timing.RepeatCount != 1)
            {
                throw new ArgumentException(
                    "A one-shot send rule must have a repeat count of one.",
                    nameof(timing));
            }

            SignalOverride[] materializedOverrides = signalOverrides.ToArray();
            if (materializedOverrides.Any(signalOverride => signalOverride is null))
            {
                throw new ArgumentException(
                    "A simulation message rule cannot contain a null signal override.",
                    nameof(signalOverrides));
            }

            bool hasDuplicateSignalOverride = materializedOverrides
                .GroupBy(signalOverride => signalOverride.SignalName, StringComparer.Ordinal)
                .Any(group => group.Count() > 1);
            if (hasDuplicateSignalOverride)
            {
                throw new ArgumentException(
                    "A simulation message rule cannot contain duplicate signal overrides.",
                    nameof(signalOverrides));
            }

            CanIdentifier = canIdentifier;
            IsExtendedIdentifier = isExtendedIdentifier;
            IsEnabled = isEnabled;
            GatewayMode = gatewayMode;
            SendType = sendType;
            Timing = timing;
            SignalOverrides = new ReadOnlyCollection<SignalOverride>(materializedOverrides);
            E2eProtection = e2eProtection;
        }

        /// <summary>
        /// Gets the normalized CAN identifier selected by the rule.
        /// </summary>
        public uint CanIdentifier { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="CanIdentifier"/> is extended.
        /// </summary>
        public bool IsExtendedIdentifier { get; }

        /// <summary>
        /// Gets a value indicating whether the rule participates in runtime processing.
        /// </summary>
        public bool IsEnabled { get; }

        /// <summary>
        /// Gets the gateway behavior for the selected message.
        /// </summary>
        public GatewayMode GatewayMode { get; }

        /// <summary>
        /// Gets the requested send behavior.
        /// </summary>
        public SimulationSendType SendType { get; }

        /// <summary>
        /// Gets the timing declaration for the rule.
        /// </summary>
        public SimulationTiming Timing { get; }

        /// <summary>
        /// Gets the signal values that are replaced during injection.
        /// </summary>
        public IReadOnlyList<SignalOverride> SignalOverrides { get; }

        /// <summary>
        /// Gets the E2E protection settings used after injection.
        /// </summary>
        public E2eProtectionConfiguration E2eProtection { get; }
    }

    /// <summary>
    /// Immutable configuration snapshot for simulation rules bound to a parsed DBC document.
    /// </summary>
    public sealed class SimulationPlan
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SimulationPlan"/> class.
        /// </summary>
        public SimulationPlan(DbcDocument document, IEnumerable<SimulationMessageRule> messageRules)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(messageRules);

            SimulationMessageRule[] materializedRules = messageRules.ToArray();
            if (materializedRules.Any(rule => rule is null))
            {
                throw new ArgumentException("A simulation plan cannot contain a null message rule.", nameof(messageRules));
            }

            bool hasDuplicateMessageRule = materializedRules
                .GroupBy(rule => (rule.CanIdentifier, rule.IsExtendedIdentifier))
                .Any(group => group.Count() > 1);
            if (hasDuplicateMessageRule)
            {
                throw new ArgumentException(
                    "A simulation plan cannot contain duplicate normalized CAN identifiers.",
                    nameof(messageRules));
            }

            var documentMessageKeys = document.Messages
                .Select(message => (message.Identifier, message.IsExtendedIdentifier))
                .ToHashSet();
            if (materializedRules.Any(rule => !documentMessageKeys.Contains(
                    (rule.CanIdentifier, rule.IsExtendedIdentifier))))
            {
                throw new ArgumentException(
                    "Every simulation message rule must reference a message in the DBC document.",
                    nameof(messageRules));
            }

            foreach (SimulationMessageRule rule in materializedRules)
            {
                DbcMessage message = document.Messages.First(message =>
                    message.Identifier == rule.CanIdentifier
                    && message.IsExtendedIdentifier == rule.IsExtendedIdentifier);
                ValidateSignalOverrides(message, rule);
            }

            Document = document;
            MessageRules = new ReadOnlyCollection<SimulationMessageRule>(materializedRules);
        }

        /// <summary>
        /// Gets the parsed DBC document that resolves message and signal references.
        /// </summary>
        public DbcDocument Document { get; }

        /// <summary>
        /// Gets the immutable message rule set.
        /// </summary>
        public IReadOnlyList<SimulationMessageRule> MessageRules { get; }

        private static void ValidateSignalOverrides(DbcMessage message, SimulationMessageRule rule)
        {
            foreach (SignalOverride signalOverride in rule.SignalOverrides)
            {
                DbcSignal? signal = message.Signals.FirstOrDefault(signal =>
                    string.Equals(signal.Name, signalOverride.SignalName, StringComparison.Ordinal));
                if (signal is null)
                {
                    throw new ArgumentException(
                        "Every signal override must reference a signal in its DBC message.",
                        nameof(rule));
                }

                if (!double.IsFinite(signalOverride.PhysicalValue)
                    || signalOverride.PhysicalValue < signal.Minimum
                    || signalOverride.PhysicalValue > signal.Maximum)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(rule),
                        signalOverride.PhysicalValue,
                        "The signal override must be finite and within the DBC signal range.");
                }
            }
        }
    }
}
