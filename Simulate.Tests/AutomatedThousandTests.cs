using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;
using Simulate.ViewModels;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class AutomatedThousandTests
    {
        private static DbcDocument? _cachedDocument;

        private static DbcDocument GetOrLoadDbc()
        {
            if (_cachedDocument is not null)
            {
                return _cachedDocument;
            }

            string repoRoot = FindRepositoryRoot();
            string dbcPath = Path.Combine(repoRoot, "DBC", "VF EBUS6M_PCAN_V2.0.0_20250524.dbc");
            string content = File.ReadAllText(dbcPath);
            _cachedDocument = DbcParser.Parse(content).Document
                ?? throw new AssertFailedException("DBC fixture must parse successfully.");
            return _cachedDocument;
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Simulate.sln")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            throw new AssertFailedException("Could not locate repository root containing Simulate.sln.");
        }

        public static IEnumerable<object[]> GetSignalsForMinRoundtrip()
        {
            DbcDocument doc = GetOrLoadDbc();
            foreach (DbcMessage msg in doc.Messages)
            {
                foreach (DbcSignal sig in msg.Signals)
                {
                    yield return new object[] { msg.Name, sig.Name, true };
                }
            }
        }

        public static IEnumerable<object[]> GetSignalsForMaxRoundtrip()
        {
            DbcDocument doc = GetOrLoadDbc();
            foreach (DbcMessage msg in doc.Messages)
            {
                foreach (DbcSignal sig in msg.Signals)
                {
                    yield return new object[] { msg.Name, sig.Name, false };
                }
            }
        }

        public static IEnumerable<object[]> GetTimeSpanFuzzCases()
        {
            string[] units = ["ms", "s", "MS", "S", " ms", " s", ""];
            double[] values = [0, 1, 5, 10, 20, 50, 100, 250, 500, 1000, 2500, 5000, 10000];
            int count = 0;
            foreach (double val in values)
            {
                foreach (string unit in units)
                {
                    if (count >= 70) break;
                    yield return new object[] { $"{val}{unit}", val, unit };
                    count++;
                }
                if (count >= 70) break;
            }

            string[] edgeCases = [
                "", " ", "   ", "abc", "ms", "s", "-10 ms", "-5 s", "100ms_extra",
                "0.5 s", "0.25 s", "1.5 s", "2.0 s", "0.0 s", "100.5 ms", "250.25 ms",
                "999999 ms", "1e2 ms", "NaN ms", "Infinity s", "null", "none",
                "10 s 20 ms", "--10 ms", "+50 ms", "+2 s", "10s", "500ms", "0ms", "0s"
            ];
            foreach (string edge in edgeCases)
            {
                yield return new object[] { edge, 0.0, "edge" };
            }
        }

        public static IEnumerable<object[]> GetFaultValueFuzzCases()
        {
            string[] testValues = [
                "0x0", "0x1", "0x2", "0x5", "0xA", "0xF", "0x10", "0x20", "0x50", "0x7F",
                "0x80", "0xFF", "0x100", "0x200", "0x500", "0x7FF", "0x800", "0xFFF", "0x1000", "0xFFFF",
                "0x10000", "0x7FFFFFFF", "0xFFFFFFFF", "0x0000", "0x00FF", "0x00000000",
                "0", "1", "2", "5", "10", "20", "50", "100", "250", "500", "1000",
                "-1", "-5", "-10", "-40", "-50", "-100", "-500", "-1000",
                "0.0", "0.5", "1.25", "2.5", "10.75", "55.5", "123.456", "-0.5", "-12.5", "-40.0",
                "", " ", "xyz", "0xZZZ", "0x"
            ];
            for (int i = 0; i < testValues.Length; i++)
            {
                yield return new object[] { testValues[i], i };
            }
        }

        public static IEnumerable<object[]> GetPlanMessageRuleCases()
        {
            DbcDocument doc = GetOrLoadDbc();
            for (int i = 0; i < doc.Messages.Count; i++)
            {
                DbcMessage msg = doc.Messages[i];
                yield return new object[] { msg.Name, msg.Identifier, msg.IsExtendedIdentifier, i % 3 };
            }
        }

        public static IEnumerable<object[]> GetCommandStateTransitionCases()
        {
            for (int i = 0; i < 40; i++)
            {
                yield return new object[] { i };
            }
        }

        // --- 1. 370 Tests: Minimum Value Pack and Unpack Roundtrip ---
        [DataTestMethod]
        [DynamicData(nameof(GetSignalsForMinRoundtrip), DynamicDataSourceType.Method)]
        public void Test_01_Signal_Minimum_Roundtrip(string messageName, string signalName, bool isMin)
        {
            DbcDocument doc = GetOrLoadDbc();
            DbcMessage msg = doc.Messages.First(m => m.Name == messageName);
            DbcSignal sig = msg.Signals.First(s => s.Name == signalName);

            byte[] payload = new byte[Math.Max(msg.PayloadLength, 8)];
            double targetVal = isMin ? sig.Minimum : sig.Maximum;

            try
            {
                SignalCodec.PackPhysical(payload, sig, targetVal);
                double unpacked = SignalCodec.UnpackPhysical(payload, sig);

                double tolerance = Math.Abs(sig.Factor) + 1e-3;
                Assert.AreEqual(targetVal, unpacked, tolerance, $"Signal {messageName}.{signalName} min roundtrip mismatch.");
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Assert.IsTrue(ex.Message.Contains("physical value") || ex.Message.Contains("cannot be represented"),
                    $"Unexpected exception for {messageName}.{signalName}: {ex.Message}");
            }
        }

        // --- 2. 370 Tests: Maximum Value Pack and Unpack Roundtrip ---
        [DataTestMethod]
        [DynamicData(nameof(GetSignalsForMaxRoundtrip), DynamicDataSourceType.Method)]
        public void Test_02_Signal_Maximum_Roundtrip(string messageName, string signalName, bool isMin)
        {
            DbcDocument doc = GetOrLoadDbc();
            DbcMessage msg = doc.Messages.First(m => m.Name == messageName);
            DbcSignal sig = msg.Signals.First(s => s.Name == signalName);

            byte[] payload = new byte[Math.Max(msg.PayloadLength, 8)];
            double targetVal = isMin ? sig.Minimum : sig.Maximum;

            try
            {
                SignalCodec.PackPhysical(payload, sig, targetVal);
                double unpacked = SignalCodec.UnpackPhysical(payload, sig);

                double tolerance = Math.Abs(sig.Factor) + 1e-3;
                Assert.AreEqual(targetVal, unpacked, tolerance, $"Signal {messageName}.{signalName} max roundtrip mismatch.");
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Assert.IsTrue(ex.Message.Contains("physical value") || ex.Message.Contains("cannot be represented"),
                    $"Unexpected exception for {messageName}.{signalName}: {ex.Message}");
            }
        }

        // --- 3. 100 Tests: TimeSpan Parsing Fuzzing ---
        [DataTestMethod]
        [DynamicData(nameof(GetTimeSpanFuzzCases), DynamicDataSourceType.Method)]
        public void Test_03_TimeSpan_Parser_Fuzzing(string input, double expectedVal, string unitType)
        {
            TimeSpan fallback = TimeSpan.FromMilliseconds(100);
            TimeSpan result = FaultConfigurationViewModel.ParseTimeSpan(input, fallback);

            Assert.IsTrue(result >= TimeSpan.Zero, $"ParseTimeSpan must never return negative TimeSpan for input: {input}");

            if (unitType == "ms" && expectedVal >= 0)
            {
                Assert.AreEqual(expectedVal, result.TotalMilliseconds, 1e-3);
            }
            else if (unitType == "s" && expectedVal >= 0)
            {
                Assert.AreEqual(expectedVal, result.TotalSeconds, 1e-3);
            }
        }

        // --- 4. 60 Tests: Fault Value Parsing Fuzzing ---
        [DataTestMethod]
        [DynamicData(nameof(GetFaultValueFuzzCases), DynamicDataSourceType.Method)]
        public void Test_04_FaultValue_Parser_Fuzzing(string input, int caseIndex)
        {
            var queue = new System.Collections.ObjectModel.ObservableCollection<FaultQueueModel>();
            var vm = new FaultConfigurationViewModel(queue)
            {
                FaultValueText = input
            };

            double parsed = vm.GetFaultValue(fallback: -999.0);
            Assert.IsTrue(double.IsFinite(parsed), $"GetFaultValue must return finite value for case {caseIndex}: '{input}'");
        }

        // --- 5. 61 Tests: Simulation Plan Creation For Every DBC Message ---
        [DataTestMethod]
        [DynamicData(nameof(GetPlanMessageRuleCases), DynamicDataSourceType.Method)]
        public void Test_05_SimulationPlan_MessageRule_PerMessage(string messageName, uint canId, bool isExtended, int modeSelector)
        {
            DbcDocument doc = GetOrLoadDbc();
            SimulationSendType sendType = modeSelector switch
            {
                0 => SimulationSendType.Cyclic,
                1 => SimulationSendType.OneShot,
                _ => SimulationSendType.Event
            };

            SimulationTiming timing = sendType switch
            {
                SimulationSendType.Cyclic => new SimulationTiming(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), 0),
                SimulationSendType.OneShot => new SimulationTiming(TimeSpan.Zero, null, 1),
                _ => new SimulationTiming(TimeSpan.Zero, null, 0)
            };

            var rule = new SimulationMessageRule(
                canId,
                isExtended,
                isEnabled: true,
                GatewayMode.PassThrough,
                sendType,
                timing,
                signalOverrides: [],
                new E2eProtectionConfiguration(false));

            var plan = new SimulationPlan(doc, [rule]);
            Assert.AreEqual(1, plan.MessageRules.Count);
            Assert.AreEqual(canId, plan.MessageRules[0].CanIdentifier);
        }

        // --- 6. 40 Tests: Execution Control Rapid State Transitions ---
        [DataTestMethod]
        [DynamicData(nameof(GetCommandStateTransitionCases), DynamicDataSourceType.Method)]
        public async Task Test_06_ExecutionControl_StateTransitions(int caseIndex)
        {
            DbcDocument doc = GetOrLoadDbc();
            var engine = new MockFastEngine();
            using var vm = new SimulationViewModel(new SimulationPlan(doc, []), engine);

            switch (caseIndex % 5)
            {
                case 0:
                    // Start -> Stop
                    await vm.StartInjectionCommand.ExecuteAsync(null);
                    Assert.AreEqual("Running", vm.QueueStatusText);
                    await vm.StopInjectionCommand.ExecuteAsync(null);
                    Assert.AreEqual("Idle", vm.QueueStatusText);
                    break;

                case 1:
                    // Start -> Pause -> Resume -> Stop
                    await vm.StartInjectionCommand.ExecuteAsync(null);
                    vm.TogglePauseInjectionCommand.Execute(null);
                    Assert.AreEqual("Paused", vm.QueueStatusText);
                    vm.TogglePauseInjectionCommand.Execute(null);
                    Assert.AreEqual("Running", vm.QueueStatusText);
                    await vm.StopInjectionCommand.ExecuteAsync(null);
                    Assert.AreEqual("Idle", vm.QueueStatusText);
                    break;

                case 2:
                    // Start -> Pause -> Stop
                    await vm.StartInjectionCommand.ExecuteAsync(null);
                    vm.TogglePauseInjectionCommand.Execute(null);
                    Assert.AreEqual("Paused", vm.QueueStatusText);
                    await vm.StopInjectionCommand.ExecuteAsync(null);
                    Assert.AreEqual("Idle", vm.QueueStatusText);
                    break;

                case 3:
                    // Clear queue when idle
                    vm.FaultQueue.Add(new FaultQueueModel { Index = caseIndex, Signal = "Sig" });
                    Assert.AreEqual("1", vm.QueueItemsDisplay);
                    vm.ClearQueueCommand.Execute(null);
                    Assert.AreEqual("0", vm.QueueItemsDisplay);
                    break;

                case 4:
                    // Rapid click guard (cannot double start)
                    await vm.StartInjectionCommand.ExecuteAsync(null);
                    Assert.IsFalse(vm.CanStartInjection);
                    await vm.StopInjectionCommand.ExecuteAsync(null);
                    Assert.IsTrue(vm.CanStartInjection);
                    break;
            }
        }

        private sealed class MockFastEngine : ISimulationEngine
        {
            public bool IsRunning { get; set; }
            public bool IsScheduling { get; set; }
            public bool IsSchedulingPaused { get; set; }
            public GatewayStatistics Statistics => GatewayStatistics.Empty;
            public HardwareFailure? LastFailure => null;

            public void ReplaceSignalOverrides(uint canId, bool isExtended, IEnumerable<SignalOverride> overrides)
            {
            }

#pragma warning disable CS0067
            public event Action<RoutedCanFrame>? FrameRouted;
#pragma warning restore CS0067

            public void UpdatePlan(SimulationPlan plan)
            {
            }

            public ValueTask StartAsync(CancellationToken cancellationToken = default)
            {
                IsRunning = true;
                return ValueTask.CompletedTask;
            }

            public ValueTask StartSchedulingAsync(CancellationToken cancellationToken = default)
            {
                IsScheduling = true;
                return ValueTask.CompletedTask;
            }

            public void PauseScheduling()
            {
                IsSchedulingPaused = true;
            }

            public void ResumeScheduling()
            {
                IsSchedulingPaused = false;
            }

            public ValueTask StopSchedulingAsync()
            {
                IsScheduling = false;
                IsSchedulingPaused = false;
                return ValueTask.CompletedTask;
            }

            public ValueTask<SimulationEventTriggerResult> TriggerEventAsync(uint canId, bool isExtended, CancellationToken token = default)
            {
                return ValueTask.FromResult(SimulationEventTriggerResult.Transmitted);
            }

            public ValueTask StopAsync()
            {
                IsRunning = false;
                IsScheduling = false;
                IsSchedulingPaused = false;
                return ValueTask.CompletedTask;
            }

            public ValueTask<HardwareOperationResult> EmergencyStopAsync()
            {
                return ValueTask.FromResult(HardwareOperationResult.Succeeded());
            }

            public ValueTask DisposeAsync()
            {
                IsRunning = false;
                return ValueTask.CompletedTask;
            }
        }
    }
}
