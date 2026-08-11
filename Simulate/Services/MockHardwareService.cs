using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Simulate.Models;

namespace Simulate.Services
{
    public enum MockHardwareFaultPoint
    {
        DiscoverInterfaces,
        OpenDriver,
        OpenSession,
        ConfigureSession,
        ActivateSession,
        Transmit
    }

    public sealed class MockHardwareFaultPlan
    {
        private readonly HashSet<MockHardwareFaultPoint> _faultPoints;

        public MockHardwareFaultPlan(params MockHardwareFaultPoint[] faultPoints)
        {
            ArgumentNullException.ThrowIfNull(faultPoints);
            _faultPoints = new HashSet<MockHardwareFaultPoint>(faultPoints);
        }

        public bool Includes(MockHardwareFaultPoint faultPoint)
        {
            return _faultPoints.Contains(faultPoint);
        }
    }

    public class MockHardwareService : ICanHardwareDriver
    {
        private readonly MockHardwareFaultPlan _faultPlan;

        public MockHardwareService(MockHardwareFaultPlan? faultPlan = null)
        {
            _faultPlan = faultPlan ?? new MockHardwareFaultPlan();
        }

        private static List<HardwareInterface> CreateAvailableInterfaces()
        {
            var virtualCan = new HardwareInterface
            {
                Name = "Virtual CAN Bus",
                Channels = new List<HardwareChannel>
                {
                    new HardwareChannel { Name = "Virtual CAN 1", ChannelIndex = 0, ChannelMask = 1 },
                    new HardwareChannel { Name = "Virtual CAN 2", ChannelIndex = 1, ChannelMask = 2 }
                }
            };

            var vn1640 = new HardwareInterface
            {
                Name = "Vector VN1640",
                Channels = new List<HardwareChannel>
                {
                    new HardwareChannel { Name = "VN1640 Channel 1", ChannelIndex = 2, ChannelMask = 4 },
                    new HardwareChannel { Name = "VN1640 Channel 2", ChannelIndex = 3, ChannelMask = 8 }
                }
            };

            return new List<HardwareInterface> { virtualCan, vn1640 };
        }

        public Task<HardwareOperationResult<IReadOnlyList<HardwareInterface>>> DiscoverInterfacesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_faultPlan.Includes(MockHardwareFaultPoint.DiscoverInterfaces))
            {
                return Task.FromResult(HardwareOperationResult.Failed<IReadOnlyList<HardwareInterface>>(
                    CreateFaultFailure(MockHardwareFaultPoint.DiscoverInterfaces)));
            }

            IReadOnlyList<HardwareInterface> interfaces = CreateAvailableInterfaces();
            return Task.FromResult(HardwareOperationResult.Succeeded(interfaces));
        }

        public Task<HardwareOperationResult<ICanGatewaySession>> OpenGatewaySessionAsync(
            CanGatewayOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            cancellationToken.ThrowIfCancellationRequested();

            HardwareFailure? failure = GetOpenFailure();
            if (failure is not null)
            {
                return Task.FromResult(HardwareOperationResult.Failed<ICanGatewaySession>(failure));
            }

            ICanGatewaySession session = new MockCanGatewaySession(options, _faultPlan);
            return Task.FromResult(HardwareOperationResult.Succeeded(session));
        }

        private HardwareFailure? GetOpenFailure()
        {
            MockHardwareFaultPoint[] openFaultPoints =
            {
                MockHardwareFaultPoint.OpenDriver,
                MockHardwareFaultPoint.OpenSession,
                MockHardwareFaultPoint.ConfigureSession,
                MockHardwareFaultPoint.ActivateSession
            };

            foreach (MockHardwareFaultPoint faultPoint in openFaultPoints)
            {
                if (_faultPlan.Includes(faultPoint))
                {
                    return CreateFaultFailure(faultPoint);
                }
            }

            return null;
        }

        internal static HardwareFailure CreateFaultFailure(MockHardwareFaultPoint faultPoint)
        {
            return faultPoint switch
            {
                MockHardwareFaultPoint.DiscoverInterfaces => new HardwareFailure(
                    HardwareOperation.DiscoverInterfaces,
                    HardwareErrorCode.DiscoveryFailed,
                    "Mock discovery failure."),
                MockHardwareFaultPoint.OpenDriver => new HardwareFailure(
                    HardwareOperation.OpenDriver,
                    HardwareErrorCode.DriverUnavailable,
                    "Mock driver-open failure."),
                MockHardwareFaultPoint.OpenSession => new HardwareFailure(
                    HardwareOperation.OpenSession,
                    HardwareErrorCode.OpenFailed,
                    "Mock session-open failure."),
                MockHardwareFaultPoint.ConfigureSession => new HardwareFailure(
                    HardwareOperation.ConfigureSession,
                    HardwareErrorCode.ConfigurationFailed,
                    "Mock session-configuration failure."),
                MockHardwareFaultPoint.ActivateSession => new HardwareFailure(
                    HardwareOperation.ActivateSession,
                    HardwareErrorCode.ActivationFailed,
                    "Mock session-activation failure."),
                MockHardwareFaultPoint.Transmit => new HardwareFailure(
                    HardwareOperation.Transmit,
                    HardwareErrorCode.TransmitFailed,
                    "Mock transmit failure."),
                _ => throw new ArgumentOutOfRangeException(nameof(faultPoint), faultPoint, "Unknown mock fault point.")
            };
        }
    }

    public sealed class MockCanGatewaySession : ICanGatewaySession
    {
        private readonly Channel<RoutedCanFrame> _receivedFrames = Channel.CreateUnbounded<RoutedCanFrame>();
        private readonly Channel<RoutedCanFrame> _transmittedFrames = Channel.CreateUnbounded<RoutedCanFrame>();
        private readonly MockHardwareFaultPlan _faultPlan;
        private int _isStopped;

        public MockCanGatewaySession(CanGatewayOptions options, MockHardwareFaultPlan faultPlan)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            _faultPlan = faultPlan ?? throw new ArgumentNullException(nameof(faultPlan));
        }

        public CanGatewayOptions Options { get; }

        public bool IsOpen => Volatile.Read(ref _isStopped) == 0;

        public ValueTask<HardwareOperationResult> EnqueueReceivedAsync(
            CanGatewaySide source,
            CanFrame frame,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(frame);
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsOpen)
            {
                return ValueTask.FromResult(CreateSessionNotOpenFailure(HardwareOperation.Receive));
            }

            return _receivedFrames.Writer.TryWrite(new RoutedCanFrame(source, frame, DateTimeOffset.UtcNow))
                ? ValueTask.FromResult(HardwareOperationResult.Succeeded())
                : ValueTask.FromResult(CreateSessionNotOpenFailure(HardwareOperation.Receive));
        }

        public async IAsyncEnumerable<RoutedCanFrame> ReceiveAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (RoutedCanFrame frame in _receivedFrames.Reader.ReadAllAsync(cancellationToken))
            {
                yield return frame;
            }
        }

        public ValueTask<HardwareOperationResult> TransmitAsync(
            CanGatewaySide destination,
            CanFrame frame,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(frame);
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsOpen)
            {
                return ValueTask.FromResult(CreateSessionNotOpenFailure(HardwareOperation.Transmit));
            }

            if (Options.BusMode == CanBusMode.Classic && frame.Format == CanFrameFormat.FlexibleDataRate)
            {
                var failure = new HardwareFailure(
                    HardwareOperation.Transmit,
                    HardwareErrorCode.InvalidConfiguration,
                    "A Classic CAN session cannot transmit a CAN FD frame.");
                return ValueTask.FromResult(HardwareOperationResult.Failed(failure));
            }

            if (_faultPlan.Includes(MockHardwareFaultPoint.Transmit))
            {
                return ValueTask.FromResult(HardwareOperationResult.Failed(
                    MockHardwareService.CreateFaultFailure(MockHardwareFaultPoint.Transmit)));
            }

            return _transmittedFrames.Writer.TryWrite(new RoutedCanFrame(destination, frame, DateTimeOffset.UtcNow))
                ? ValueTask.FromResult(HardwareOperationResult.Succeeded())
                : ValueTask.FromResult(CreateSessionNotOpenFailure(HardwareOperation.Transmit));
        }

        public async IAsyncEnumerable<RoutedCanFrame> ReceiveTransmittedAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (RoutedCanFrame frame in _transmittedFrames.Reader.ReadAllAsync(cancellationToken))
            {
                yield return frame;
            }
        }

        public ValueTask<HardwareOperationResult> FlushAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return IsOpen
                ? ValueTask.FromResult(HardwareOperationResult.Succeeded())
                : ValueTask.FromResult(CreateSessionNotOpenFailure(HardwareOperation.Flush));
        }

        public ValueTask<HardwareOperationResult> StopAsync()
        {
            if (Interlocked.Exchange(ref _isStopped, 1) == 0)
            {
                _receivedFrames.Writer.TryComplete();
                _transmittedFrames.Writer.TryComplete();
            }

            return ValueTask.FromResult(HardwareOperationResult.Succeeded());
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }

        private static HardwareOperationResult CreateSessionNotOpenFailure(HardwareOperation operation)
        {
            var failure = new HardwareFailure(
                operation,
                HardwareErrorCode.SessionNotOpen,
                "The CAN gateway session is not open.");
            return HardwareOperationResult.Failed(failure);
        }
    }
}
