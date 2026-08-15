using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;
using Simulate.ViewModels;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class SimulationViewModelTests
    {
        [TestMethod]
        public void Default_instance_exposes_empty_runtime_state()
        {
            var viewModel = new SimulationViewModel();

            viewModel.RefreshRuntimeState();

            Assert.IsFalse(viewModel.IsConfigured);
            Assert.IsFalse(viewModel.IsRunning);
            Assert.IsFalse(viewModel.IsScheduling);
            Assert.IsFalse(viewModel.IsSchedulingPaused);
            Assert.AreSame(GatewayStatistics.Empty, viewModel.Statistics);
            Assert.IsNull(viewModel.LastFailure);
        }

        [TestMethod]
        public void Create_projects_dbc_messages_signals_and_configured_overrides()
        {
            const string documentText = """
                BO_ 291 VehicleData: 2 Gateway
                 SG_ VehicleSpeed : 0|8@1+ (0.5,0) [0|127.5] "km/h" Gateway
                 SG_ VehicleMode : 8|4@1+ (1,0) [0|15] "" Gateway
                BO_ 419 BrakeStatus: 1 Gateway
                 SG_ BrakeApplied : 0|1@1+ (1,0) [0|1] "" Gateway
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
                    startDelay: TimeSpan.FromMilliseconds(20),
                    cycleInterval: TimeSpan.FromMilliseconds(100),
                    repeatCount: 0),
                signalOverrides: [new SignalOverride("VehicleSpeed", 55d)],
                e2eProtection: new E2eProtectionConfiguration(isEnabled: false));
            var engine = new FakeSimulationEngine();

            var viewModel = new SimulationViewModel(new SimulationPlan(document, [rule]), engine);

            Assert.AreEqual(2, viewModel.Messages.Count);
            MessageModel message = viewModel.Messages.Single(item => item.Name == "VehicleData");
            Assert.AreEqual("0x123", message.Id);
            Assert.AreEqual(2, message.Dlc);
            Assert.AreEqual("100 ms", message.Cycle);
            Assert.AreEqual("Inject", message.GatewayMode);
            Assert.AreEqual("Cyclic", message.SendType);
            Assert.AreEqual(2, message.SignalCount);

            Assert.AreEqual(3, viewModel.Signals.Count);
            SignalModel signal = viewModel.Signals.Single(item => item.Name == "VehicleSpeed");
            Assert.AreEqual("VehicleData", signal.MessageName);
            Assert.AreEqual(8, signal.Length);
            Assert.AreEqual(55d, signal.Value);
            Assert.IsTrue(signal.IsOverridden);

            Assert.AreEqual(1, viewModel.FaultQueue.Count);
            FaultQueueModel fault = viewModel.FaultQueue[0];
            Assert.AreEqual(1, fault.Index);
            Assert.AreEqual("0x123", fault.MsgId);
            Assert.AreEqual("VehicleData", fault.MsgName);
            Assert.AreEqual("VehicleSpeed", fault.Signal);
            Assert.AreEqual("55 km/h", fault.FaultValue);
            Assert.AreEqual("Inject", fault.Mode);
            Assert.AreEqual("Configured", fault.Status);
        }

        [TestMethod]
        public void Refresh_runtime_state_reads_the_engine_without_starting_it()
        {
            DbcDocument document = ParseSingleMessageDocument();
            HardwareFailure failure = CreateFailure();
            var engine = new FakeSimulationEngine
            {
                IsRunning = true,
                IsScheduling = true,
                IsSchedulingPaused = true,
                LastFailure = failure
            };
            var viewModel = new SimulationViewModel(new SimulationPlan(document, []), engine);

            viewModel.RefreshRuntimeState();

            Assert.IsTrue(viewModel.IsRunning);
            Assert.IsTrue(viewModel.IsScheduling);
            Assert.IsTrue(viewModel.IsSchedulingPaused);
            Assert.AreSame(engine.Statistics, viewModel.Statistics);
            Assert.AreSame(failure, viewModel.LastFailure);
            Assert.AreEqual(0, engine.StartCallCount);
            Assert.AreEqual(0, engine.StartSchedulingCallCount);
        }

        [TestMethod]
        public async Task Explicit_start_forwards_to_the_engine_and_refreshes_runtime_state()
        {
            DbcDocument document = ParseSingleMessageDocument();
            var engine = new FakeSimulationEngine();
            var viewModel = new SimulationViewModel(new SimulationPlan(document, []), engine);

            await viewModel.StartAsync();
            await viewModel.StartSchedulingAsync();

            Assert.AreEqual(1, engine.StartCallCount);
            Assert.AreEqual(1, engine.StartSchedulingCallCount);
            Assert.IsTrue(viewModel.IsRunning);
            Assert.IsTrue(viewModel.IsScheduling);
        }

        [TestMethod]
        public async Task Explicit_runtime_controls_forward_to_the_engine_without_commands()
        {
            DbcDocument document = ParseSingleMessageDocument();
            var engine = new FakeSimulationEngine
            {
                IsRunning = true,
                IsScheduling = true
            };
            var viewModel = new SimulationViewModel(new SimulationPlan(document, []), engine);

            viewModel.PauseScheduling();
            viewModel.ResumeScheduling();
            await viewModel.StopSchedulingAsync();
            await viewModel.StopAsync();
            HardwareOperationResult emergencyResult = await viewModel.EmergencyStopAsync();

            Assert.AreEqual(1, engine.PauseSchedulingCallCount);
            Assert.AreEqual(1, engine.ResumeSchedulingCallCount);
            Assert.AreEqual(1, engine.StopSchedulingCallCount);
            Assert.AreEqual(1, engine.StopCallCount);
            Assert.AreEqual(1, engine.EmergencyStopCallCount);
            Assert.IsTrue(emergencyResult.IsSuccess);
            Assert.IsFalse(viewModel.IsRunning);
            Assert.IsFalse(viewModel.IsScheduling);
            Assert.IsFalse(viewModel.IsSchedulingPaused);
        }

        [TestMethod]
        public void Stop_async_refreshes_observable_state_on_the_caller_synchronization_context()
        {
            DbcDocument document = ParseSingleMessageDocument();
            var engine = new DelayedStopSimulationEngine { IsRunning = true };
            var viewModel = new SimulationViewModel(new SimulationPlan(document, []), engine);
            var synchronizationContext = new QueueingSynchronizationContext();
            SynchronizationContext? previousContext = SynchronizationContext.Current;
            int callerThreadId = Environment.CurrentManagedThreadId;
            int notificationThreadId = -1;
            viewModel.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(viewModel.IsRunning))
                {
                    notificationThreadId = Environment.CurrentManagedThreadId;
                }
            };

            try
            {
                SynchronizationContext.SetSynchronizationContext(synchronizationContext);
                Task stop = viewModel.StopAsync().AsTask();
                Assert.IsTrue(engine.StopRequested.Task.Wait(TimeSpan.FromSeconds(1)));

                engine.CompleteStop();

                Assert.IsTrue(synchronizationContext.WaitForPost(TimeSpan.FromSeconds(1)));
                synchronizationContext.RunNext();
                stop.GetAwaiter().GetResult();

                Assert.AreEqual(callerThreadId, notificationThreadId);
                Assert.IsFalse(viewModel.IsRunning);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        [TestMethod]
        public async Task Stop_async_refreshes_failure_state_and_rethrows_the_original_exception()
        {
            DbcDocument document = ParseSingleMessageDocument();
            HardwareFailure failure = CreateFailure();
            var expectedException = new HardwareOperationException(failure);
            var engine = new FakeSimulationEngine
            {
                IsRunning = true,
                StopFailure = expectedException
            };
            var viewModel = new SimulationViewModel(new SimulationPlan(document, []), engine);

            HardwareOperationException exception = await Assert.ThrowsExceptionAsync<HardwareOperationException>(
                async () => await viewModel.StopAsync());

            Assert.AreSame(expectedException, exception);
            Assert.IsFalse(viewModel.IsRunning);
            Assert.AreSame(failure, viewModel.LastFailure);
        }

        [TestMethod]
        public async Task Stop_scheduling_async_refreshes_failure_state_and_rethrows_the_original_exception()
        {
            DbcDocument document = ParseSingleMessageDocument();
            HardwareFailure failure = CreateFailure();
            var expectedException = new HardwareOperationException(failure);
            var engine = new FakeSimulationEngine
            {
                IsScheduling = true,
                IsSchedulingPaused = true,
                StopSchedulingFailure = expectedException
            };
            var viewModel = new SimulationViewModel(new SimulationPlan(document, []), engine);

            HardwareOperationException exception = await Assert.ThrowsExceptionAsync<HardwareOperationException>(
                async () => await viewModel.StopSchedulingAsync());

            Assert.AreSame(expectedException, exception);
            Assert.IsFalse(viewModel.IsScheduling);
            Assert.IsFalse(viewModel.IsSchedulingPaused);
            Assert.AreSame(failure, viewModel.LastFailure);
        }

        [TestMethod]
        public void Main_view_model_forwards_existing_collection_binding_paths()
        {
            DbcDocument document = ParseSingleMessageDocument();
            var simulation = new SimulationViewModel(
                new SimulationPlan(document, []),
                new FakeSimulationEngine());
            var main = new MainViewModel(
                new ConnectionViewModel(new MockHardwareService()),
                simulation);

            Assert.AreSame(simulation.Messages, main.Messages);
            Assert.AreSame(simulation.Signals, main.Signals);
            Assert.AreSame(simulation.FaultQueue, main.FaultQueue);
            Assert.AreEqual("VehicleData", main.Messages[0].Name);
            Assert.AreEqual("VehicleSpeed", main.Signals[0].Name);
        }

        private static DbcDocument ParseSingleMessageDocument()
        {
            const string documentText = """
                BO_ 291 VehicleData: 1 Gateway
                 SG_ VehicleSpeed : 0|8@1+ (0.5,0) [0|127.5] "km/h" Gateway
                """;
            return DbcParser.Parse(documentText).Document
                ?? throw new AssertFailedException("The DBC fixture must parse successfully.");
        }

        private static HardwareFailure CreateFailure()
        {
            return new HardwareFailure(
                HardwareOperation.Transmit,
                HardwareErrorCode.TransmitFailed,
                "The simulated CAN operation failed.");
        }

        private sealed class FakeSimulationEngine : ISimulationEngine
        {
            public bool IsRunning { get; set; }

            public bool IsScheduling { get; set; }

            public bool IsSchedulingPaused { get; set; }

            public GatewayStatistics Statistics { get; } = GatewayStatistics.Empty;

            public HardwareFailure? LastFailure { get; set; }

            public HardwareOperationException? StopFailure { get; set; }

            public HardwareOperationException? StopSchedulingFailure { get; set; }

            public int StartCallCount { get; private set; }

            public int StartSchedulingCallCount { get; private set; }

            public int PauseSchedulingCallCount { get; private set; }

            public int ResumeSchedulingCallCount { get; private set; }

            public int StopSchedulingCallCount { get; private set; }

            public int StopCallCount { get; private set; }

            public int EmergencyStopCallCount { get; private set; }

            public void ReplaceSignalOverrides(
                uint canIdentifier,
                bool isExtendedIdentifier,
                IEnumerable<SignalOverride> signalOverrides)
            {
            }

            public ValueTask StartAsync(CancellationToken cancellationToken = default)
            {
                StartCallCount++;
                IsRunning = true;
                return ValueTask.CompletedTask;
            }

            public ValueTask StartSchedulingAsync(CancellationToken cancellationToken = default)
            {
                StartSchedulingCallCount++;
                IsScheduling = true;
                return ValueTask.CompletedTask;
            }

            public void PauseScheduling()
            {
                PauseSchedulingCallCount++;
                IsSchedulingPaused = true;
            }

            public void ResumeScheduling()
            {
                ResumeSchedulingCallCount++;
                IsSchedulingPaused = false;
            }

            public ValueTask StopSchedulingAsync()
            {
                StopSchedulingCallCount++;
                IsScheduling = false;
                IsSchedulingPaused = false;
                if (StopSchedulingFailure is not null)
                {
                    LastFailure = StopSchedulingFailure.Failure;
                    return ValueTask.FromException(StopSchedulingFailure);
                }

                return ValueTask.CompletedTask;
            }

            public ValueTask<SimulationEventTriggerResult> TriggerEventAsync(
                uint canIdentifier,
                bool isExtendedIdentifier,
                CancellationToken cancellationToken = default)
            {
                return ValueTask.FromResult(SimulationEventTriggerResult.Transmitted);
            }

            public ValueTask StopAsync()
            {
                StopCallCount++;
                IsRunning = false;
                if (StopFailure is not null)
                {
                    LastFailure = StopFailure.Failure;
                    return ValueTask.FromException(StopFailure);
                }

                return ValueTask.CompletedTask;
            }

            public ValueTask<HardwareOperationResult> EmergencyStopAsync()
            {
                EmergencyStopCallCount++;
                IsRunning = false;
                IsScheduling = false;
                IsSchedulingPaused = false;
                return ValueTask.FromResult(HardwareOperationResult.Succeeded());
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }

        private sealed class DelayedStopSimulationEngine : ISimulationEngine
        {
            private readonly TaskCompletionSource _stopCompletion = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public bool IsRunning { get; set; }

            public bool IsScheduling { get; private set; }

            public bool IsSchedulingPaused { get; private set; }

            public GatewayStatistics Statistics { get; } = GatewayStatistics.Empty;

            public HardwareFailure? LastFailure { get; set; }

            public TaskCompletionSource StopRequested { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public void ReplaceSignalOverrides(
                uint canIdentifier,
                bool isExtendedIdentifier,
                IEnumerable<SignalOverride> signalOverrides)
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

            public ValueTask<SimulationEventTriggerResult> TriggerEventAsync(
                uint canIdentifier,
                bool isExtendedIdentifier,
                CancellationToken cancellationToken = default)
            {
                return ValueTask.FromResult(SimulationEventTriggerResult.Transmitted);
            }

            public ValueTask StopAsync()
            {
                StopRequested.TrySetResult();
                return new ValueTask(_stopCompletion.Task);
            }

            public ValueTask<HardwareOperationResult> EmergencyStopAsync()
            {
                IsRunning = false;
                return ValueTask.FromResult(HardwareOperationResult.Succeeded());
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }

            public void CompleteStop()
            {
                IsRunning = false;
                _stopCompletion.TrySetResult();
            }
        }

        private sealed class QueueingSynchronizationContext : SynchronizationContext
        {
            private readonly object _sync = new();
            private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
            private readonly ManualResetEventSlim _callbackQueued = new(initialState: false);

            public override void Post(SendOrPostCallback callback, object? state)
            {
                lock (_sync)
                {
                    _callbacks.Enqueue((callback, state));
                }

                _callbackQueued.Set();
            }

            public bool WaitForPost(TimeSpan timeout)
            {
                return _callbackQueued.Wait(timeout);
            }

            public void RunNext()
            {
                (SendOrPostCallback Callback, object? State) callback;
                lock (_sync)
                {
                    callback = _callbacks.Dequeue();
                    if (_callbacks.Count == 0)
                    {
                        _callbackQueued.Reset();
                    }
                }

                callback.Callback(callback.State);
            }
        }
    }
}
