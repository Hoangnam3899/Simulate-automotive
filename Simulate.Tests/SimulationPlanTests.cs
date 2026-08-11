using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class SimulationPlanTests
    {
        [TestMethod]
        public void Create_accepts_an_inject_rule_with_a_valid_dbc_override()
        {
            const string documentText = """
                BO_ 291 VehicleData: 2 Gateway
                 SG_ VehicleSpeed : 0|8@1+ (0.5,0) [0|127.5] "km/h" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
            var rule = new SimulationMessageRule(
                canIdentifier: 291,
                isExtendedIdentifier: false,
                isEnabled: true,
                gatewayMode: GatewayMode.Inject,
                sendType: SimulationSendType.Cyclic,
                timing: new SimulationTiming(
                    startDelay: TimeSpan.FromMilliseconds(10),
                    cycleInterval: TimeSpan.FromMilliseconds(20),
                    repeatCount: 0),
                signalOverrides: [new SignalOverride("VehicleSpeed", 55d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));

            var plan = new SimulationPlan(document, [rule]);

            Assert.AreSame(document, plan.Document);
            Assert.AreEqual(1, plan.MessageRules.Count);
            Assert.AreSame(rule, plan.MessageRules[0]);
        }

        [TestMethod]
        public void Create_rejects_duplicate_normalized_can_identifiers()
        {
            const string documentText = """
                BO_ 291 VehicleData: 2 Gateway
                 SG_ VehicleSpeed : 0|8@1+ (0.5,0) [0|127.5] "km/h" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
            var firstRule = new SimulationMessageRule(
                291,
                isExtendedIdentifier: false,
                isEnabled: true,
                GatewayMode.PassThrough,
                SimulationSendType.OneShot,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                signalOverrides: [],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            var duplicateRule = new SimulationMessageRule(
                291,
                isExtendedIdentifier: false,
                isEnabled: false,
                GatewayMode.Block,
                SimulationSendType.Event,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 0),
                signalOverrides: [],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));

            Assert.ThrowsException<ArgumentException>(
                () => new SimulationPlan(document, [firstRule, duplicateRule]));
        }

        [TestMethod]
        public void Create_rejects_a_rule_without_a_matching_dbc_message()
        {
            const string documentText = """
                BO_ 291 VehicleData: 2 Gateway
                 SG_ VehicleSpeed : 0|8@1+ (0.5,0) [0|127.5] "km/h" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
            var unknownMessageRule = new SimulationMessageRule(
                292,
                isExtendedIdentifier: false,
                isEnabled: true,
                GatewayMode.PassThrough,
                SimulationSendType.OneShot,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                signalOverrides: [],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));

            Assert.ThrowsException<ArgumentException>(
                () => new SimulationPlan(document, [unknownMessageRule]));
        }

        [TestMethod]
        public void Create_rejects_a_cyclic_rule_without_a_cycle_interval()
        {
            Assert.ThrowsException<ArgumentException>(
                () => new SimulationMessageRule(
                    291,
                    isExtendedIdentifier: false,
                    isEnabled: true,
                    GatewayMode.Inject,
                    SimulationSendType.Cyclic,
                    new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 0),
                    signalOverrides: [new SignalOverride("VehicleSpeed", 55d)],
                    e2eProtection: new E2eProtectionConfiguration(isEnabled: false)));
        }

        [DataTestMethod]
        [DataRow(-1, 10, 0)]
        [DataRow(0, 0, 0)]
        [DataRow(0, 10, -1)]
        public void Create_rejects_invalid_timing_values(
            int startDelayMilliseconds,
            int cycleIntervalMilliseconds,
            int repeatCount)
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => new SimulationTiming(
                    TimeSpan.FromMilliseconds(startDelayMilliseconds),
                    TimeSpan.FromMilliseconds(cycleIntervalMilliseconds),
                    repeatCount));
        }

        [TestMethod]
        public void Create_rejects_an_override_outside_its_dbc_signal_range()
        {
            const string documentText = """
                BO_ 291 VehicleData: 2 Gateway
                 SG_ VehicleSpeed : 0|8@1+ (0.5,0) [0|127.5] "km/h" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
            var rule = new SimulationMessageRule(
                291,
                isExtendedIdentifier: false,
                isEnabled: true,
                GatewayMode.Inject,
                SimulationSendType.OneShot,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                signalOverrides: [new SignalOverride("VehicleSpeed", 128d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => new SimulationPlan(document, [rule]));
        }

        [TestMethod]
        public void Create_rejects_a_one_shot_rule_with_a_cycle_interval()
        {
            Assert.ThrowsException<ArgumentException>(
                () => new SimulationMessageRule(
                    291,
                    isExtendedIdentifier: false,
                    isEnabled: true,
                    GatewayMode.Inject,
                    SimulationSendType.OneShot,
                    new SimulationTiming(
                        TimeSpan.Zero,
                        cycleInterval: TimeSpan.FromMilliseconds(20),
                        repeatCount: 1),
                    signalOverrides: [new SignalOverride("VehicleSpeed", 55d)],
                    e2eProtection: new E2eProtectionConfiguration(isEnabled: false)));
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(2)]
        public void Create_rejects_a_one_shot_rule_with_an_invalid_repeat_count(int repeatCount)
        {
            Assert.ThrowsException<ArgumentException>(
                () => new SimulationMessageRule(
                    291,
                    isExtendedIdentifier: false,
                    isEnabled: true,
                    GatewayMode.Inject,
                    SimulationSendType.OneShot,
                    new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount),
                    signalOverrides: [new SignalOverride("VehicleSpeed", 55d)],
                    e2eProtection: new E2eProtectionConfiguration(isEnabled: false)));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        public void Create_rejects_a_blank_override_signal_name(string signalName)
        {
            Assert.ThrowsException<ArgumentException>(
                () => new SignalOverride(signalName, 1d));
        }

        [TestMethod]
        public void Create_rejects_a_nonfinite_override_value()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => new SignalOverride("VehicleSpeed", double.NaN));
        }

        [TestMethod]
        public void Create_rejects_an_undefined_gateway_mode()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => new SimulationMessageRule(
                    291,
                    isExtendedIdentifier: false,
                    isEnabled: true,
                    (GatewayMode)99,
                    SimulationSendType.OneShot,
                    new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                    signalOverrides: [],
                    e2eProtection: new E2eProtectionConfiguration(isEnabled: false)));
        }

        [TestMethod]
        public void Create_rejects_duplicate_signal_overrides_for_one_message()
        {
            Assert.ThrowsException<ArgumentException>(
                () => new SimulationMessageRule(
                    291,
                    isExtendedIdentifier: false,
                    isEnabled: true,
                    GatewayMode.Inject,
                    SimulationSendType.OneShot,
                    new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                    signalOverrides:
                    [
                        new SignalOverride("VehicleSpeed", 55d),
                        new SignalOverride("VehicleSpeed", 60d)
                    ],
                    e2eProtection: new E2eProtectionConfiguration(isEnabled: false)));
        }

        [TestMethod]
        public void Create_rejects_an_override_without_a_matching_dbc_signal()
        {
            const string documentText = """
                BO_ 291 VehicleData: 2 Gateway
                 SG_ VehicleSpeed : 0|8@1+ (0.5,0) [0|127.5] "km/h" Gateway
                """;
            DbcDocument document = DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
            var rule = new SimulationMessageRule(
                291,
                isExtendedIdentifier: false,
                isEnabled: true,
                GatewayMode.Inject,
                SimulationSendType.OneShot,
                new SimulationTiming(TimeSpan.Zero, cycleInterval: null, repeatCount: 1),
                signalOverrides: [new SignalOverride("MissingSignal", 1d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));

            Assert.ThrowsException<ArgumentException>(
                () => new SimulationPlan(document, [rule]));
        }
    }
}
