using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Simulate.Models;

namespace Simulate.Services
{
    internal sealed class VectorNativeSessionResources
    {
        public const int InvalidPortHandle = -1;

        private readonly object _sync = new object();
        private readonly IVectorXlApi _api;
        private bool _driverIsOpen;
        private bool _portIsOpen;
        private bool _channelsAreActive;
        private int _portHandle = InvalidPortHandle;
        private ulong _accessMask;
        private HardwareOperationResult? _cleanupResult;

        public VectorNativeSessionResources(IVectorXlApi api)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public bool IsActive
        {
            get
            {
                lock (_sync)
                {
                    return _channelsAreActive && _cleanupResult is null;
                }
            }
        }

        public void OwnDriver()
        {
            _driverIsOpen = true;
        }

        public void OwnPort(int portHandle, ulong accessMask)
        {
            _portHandle = portHandle;
            _accessMask = accessMask;
            _portIsOpen = true;
        }

        public void MarkChannelsActive()
        {
            _channelsAreActive = true;
        }

        public HardwareOperationResult Cleanup()
        {
            lock (_sync)
            {
                if (_cleanupResult is not null)
                {
                    return _cleanupResult;
                }

                var diagnostics = new List<string>();
                int? firstNativeStatus = null;

                void CaptureStatus(string apiName, Func<VectorNativeStatus> nativeCall)
                {
                    try
                    {
                        VectorNativeStatus status = nativeCall();
                        if (!status.IsSuccess)
                        {
                            firstNativeStatus ??= status.Code;
                            diagnostics.Add(
                                $"{apiName} returned XL_Status {status.Name} ({status.Code}).");
                        }
                    }
                    catch (Exception exception)
                    {
                        diagnostics.Add($"{apiName} threw {exception.Message}.");
                    }
                }

                if (_channelsAreActive)
                {
                    // The Vector CAN FD flowchart (manual 20.30, p. 103) closes in this
                    // order: deactivate channels, close port, then close driver.
                    CaptureStatus(
                        "XL_DeactivateChannel",
                        () => _api.DeactivateChannels(_portHandle, _accessMask));
                    _channelsAreActive = false;
                }

                if (_portIsOpen)
                {
                    CaptureStatus("XL_ClosePort", () => _api.ClosePort(_portHandle));
                    _portIsOpen = false;
                    _portHandle = InvalidPortHandle;
                }

                if (_driverIsOpen)
                {
                    CaptureStatus("XL_CloseDriver", _api.CloseDriver);
                    _driverIsOpen = false;
                }

                if (diagnostics.Count == 0)
                {
                    _cleanupResult = HardwareOperationResult.Succeeded();
                }
                else
                {
                    var failure = new HardwareFailure(
                        HardwareOperation.Stop,
                        HardwareErrorCode.StopFailed,
                        $"Vector session cleanup failed: {string.Join(" ", diagnostics)}",
                        firstNativeStatus);
                    _cleanupResult = HardwareOperationResult.Failed(failure);
                }

                return _cleanupResult;
            }
        }
    }

    // Task 3 establishes native ownership only. Task 4/5 replace the explicit I/O
    // unavailability results below with Classic CAN and CAN FD frame operations.
    internal sealed class VectorCanGatewaySession : ICanGatewaySession
    {
        private readonly VectorNativeSessionResources _resources;

        public VectorCanGatewaySession(
            CanGatewayOptions options,
            VectorNativeSessionResources resources)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        public CanGatewayOptions Options { get; }

        public bool IsOpen => _resources.IsActive;

        public async IAsyncEnumerable<RoutedCanFrame> ReceiveAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();

            if (IsOpen)
            {
                throw new NotSupportedException(
                    "Native Vector frame reception is not available in the lifecycle adapter.");
            }

            yield break;
        }

        public ValueTask<HardwareOperationResult> TransmitAsync(
            CanGatewaySide destination,
            CanFrame frame,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(frame);
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(IsOpen
                ? CreateUnavailableFailure(
                    HardwareOperation.Transmit,
                    HardwareErrorCode.TransmitFailed,
                    "Native Vector frame transmission is not available in the lifecycle adapter.")
                : CreateSessionNotOpenFailure(HardwareOperation.Transmit));
        }

        public ValueTask<HardwareOperationResult> FlushAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(IsOpen
                ? CreateUnavailableFailure(
                    HardwareOperation.Flush,
                    HardwareErrorCode.FlushFailed,
                    "Native Vector transmit flushing is not available in the lifecycle adapter.")
                : CreateSessionNotOpenFailure(HardwareOperation.Flush));
        }

        public ValueTask<HardwareOperationResult> StopAsync()
        {
            return ValueTask.FromResult(_resources.Cleanup());
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }

        private static HardwareOperationResult CreateUnavailableFailure(
            HardwareOperation operation,
            HardwareErrorCode errorCode,
            string message)
        {
            return HardwareOperationResult.Failed(
                new HardwareFailure(operation, errorCode, message));
        }

        private static HardwareOperationResult CreateSessionNotOpenFailure(HardwareOperation operation)
        {
            return HardwareOperationResult.Failed(new HardwareFailure(
                operation,
                HardwareErrorCode.SessionNotOpen,
                "The Vector CAN gateway session is not open."));
        }
    }
}
