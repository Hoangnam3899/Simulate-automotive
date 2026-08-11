using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        public VectorNativeStatus? TransmitClassicCanFrame(
            ulong destinationMask,
            CanFrame frame,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_channelsAreActive || _cleanupResult is not null)
                {
                    return null;
                }

                return _api.TransmitClassicCanFrame(
                    _portHandle,
                    destinationMask,
                    frame.Identifier,
                    frame.IsExtendedIdentifier,
                    frame.Data);
            }
        }

        public VectorClassicReceiveBatchResult? ReceiveClassicCanEvents(
            int maximumEventCount,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_channelsAreActive || _cleanupResult is not null)
                {
                    return null;
                }

                return _api.ReceiveClassicCanEvents(_portHandle, maximumEventCount);
            }
        }

        public VectorCanFdTransmitResult? TransmitCanFdFrame(
            ulong destinationMask,
            CanFrame frame,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_channelsAreActive || _cleanupResult is not null)
                {
                    return null;
                }

                VectorCanFdEventFlags flags = frame.Format == CanFrameFormat.FlexibleDataRate
                    ? VectorCanFdEventFlags.FlexibleDataRate
                    : VectorCanFdEventFlags.None;
                if (frame.IsBitRateSwitchEnabled)
                {
                    flags |= VectorCanFdEventFlags.BitRateSwitch;
                }

                return _api.TransmitCanFdFrame(
                    _portHandle,
                    destinationMask,
                    frame.Identifier,
                    frame.IsExtendedIdentifier,
                    (byte)frame.DataLengthCode,
                    flags,
                    frame.Data);
            }
        }

        public VectorCanFdReceiveBatchResult? ReceiveCanFdEvents(
            int maximumEventCount,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_channelsAreActive || _cleanupResult is not null)
                {
                    return null;
                }

                return _api.ReceiveCanFdEvents(_portHandle, maximumEventCount);
            }
        }

        public VectorCanFlushResult? FlushCanQueues(
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_channelsAreActive || _cleanupResult is not null)
                {
                    return null;
                }

                VectorNativeStatus receiveStatus = _api.FlushReceiveQueue(_portHandle);
                VectorNativeStatus transmitStatus =
                    _api.FlushCanTransmitQueue(_portHandle, _accessMask);
                return new VectorCanFlushResult(receiveStatus, transmitStatus);
            }
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

    internal sealed class VectorCanGatewaySession : ICanGatewaySession
    {
        private const int MaximumReceiveBatchSize = 256;
        private const uint ExtendedIdentifierFlag = 0x80000000;

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
            if (Options.BusMode == CanBusMode.FlexibleDataRate)
            {
                await foreach (RoutedCanFrame frame in ReceiveCanFdAsync(cancellationToken)
                    .ConfigureAwait(false))
                {
                    yield return frame;
                }

                yield break;
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                VectorClassicReceiveBatchResult? batch;
                try
                {
                    batch = await Task.Run(
                        () => _resources.ReceiveClassicCanEvents(
                            MaximumReceiveBatchSize,
                            cancellationToken),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new HardwareOperationException(
                        new HardwareFailure(
                            HardwareOperation.Receive,
                            HardwareErrorCode.ReceiveFailed,
                            $"XL_Receive threw while receiving Classic CAN frames: {exception.Message}"),
                        exception);
                }

                if (batch is null)
                {
                    yield break;
                }

                VectorClassicReceiveBatchResult receivedBatch = batch.Value;
                foreach (VectorClassicCanEvent nativeEvent in receivedBatch.Events)
                {
                    if ((nativeEvent.Flags & VectorClassicCanEventFlags.QueueOverrun) != 0)
                    {
                        throw new HardwareOperationException(new HardwareFailure(
                            HardwareOperation.Receive,
                            HardwareErrorCode.ReceiveFailed,
                            "XL_Receive reported a Classic CAN queue overrun; one or more events were lost."));
                    }

                    if (!TryMapClassicEvent(nativeEvent, out RoutedCanFrame? routedFrame))
                    {
                        continue;
                    }

                    yield return routedFrame;
                }

                if (!receivedBatch.Status.IsSuccess && !receivedBatch.Status.IsQueueEmpty)
                {
                    throw new HardwareOperationException(new HardwareFailure(
                        HardwareOperation.Receive,
                        HardwareErrorCode.ReceiveFailed,
                        $"XL_Receive failed with XL_Status {receivedBatch.Status.Name} " +
                        $"({receivedBatch.Status.Code}).",
                        receivedBatch.Status.Code));
                }

                if (receivedBatch.Status.IsQueueEmpty)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(2), cancellationToken)
                        .ConfigureAwait(false);
                }
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
                return ValueTask.FromResult(
                    CreateSessionNotOpenFailure(HardwareOperation.Transmit));
            }

            if (Options.BusMode == CanBusMode.Classic &&
                frame.Format != CanFrameFormat.Classic)
            {
                return ValueTask.FromResult(HardwareOperationResult.Failed(
                    new HardwareFailure(
                        HardwareOperation.Transmit,
                        HardwareErrorCode.InvalidConfiguration,
                        "A Classic CAN session cannot transmit a CAN FD frame.")));
            }

            ulong destinationMask = destination switch
            {
                CanGatewaySide.Rx => Options.RxChannel.ChannelMask,
                CanGatewaySide.Tx => Options.TxChannel.ChannelMask,
                _ => 0
            };
            if (destinationMask == 0)
            {
                return ValueTask.FromResult(HardwareOperationResult.Failed(
                    new HardwareFailure(
                        HardwareOperation.Transmit,
                        HardwareErrorCode.InvalidConfiguration,
                        $"Unknown CAN gateway destination value {(int)destination}.")));
            }

            try
            {
                if (Options.BusMode == CanBusMode.FlexibleDataRate)
                {
                    VectorCanFdTransmitResult? canFdResult = _resources.TransmitCanFdFrame(
                        destinationMask,
                        frame,
                        cancellationToken);
                    if (!canFdResult.HasValue)
                    {
                        return ValueTask.FromResult(
                            CreateSessionNotOpenFailure(HardwareOperation.Transmit));
                    }

                    if (!canFdResult.Value.Status.IsSuccess)
                    {
                        return ValueTask.FromResult(CreateNativeFailure(
                            HardwareOperation.Transmit,
                            HardwareErrorCode.TransmitFailed,
                            "XL_CanTransmitEx",
                            canFdResult.Value.Status));
                    }

                    if (canFdResult.Value.MessageCountSent != 1)
                    {
                        return ValueTask.FromResult(HardwareOperationResult.Failed(
                            new HardwareFailure(
                                HardwareOperation.Transmit,
                                HardwareErrorCode.TransmitFailed,
                                "XL_CanTransmitEx succeeded but did not report exactly one transmitted frame.")));
                    }

                    return ValueTask.FromResult(HardwareOperationResult.Succeeded());
                }

                VectorNativeStatus? status = _resources.TransmitClassicCanFrame(
                    destinationMask,
                    frame,
                    cancellationToken);
                if (!status.HasValue)
                {
                    return ValueTask.FromResult(
                        CreateSessionNotOpenFailure(HardwareOperation.Transmit));
                }

                return ValueTask.FromResult(status.Value.IsSuccess
                    ? HardwareOperationResult.Succeeded()
                    : CreateNativeFailure(
                        HardwareOperation.Transmit,
                        HardwareErrorCode.TransmitFailed,
                        "XL_CanTransmit",
                        status.Value));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                string apiName = Options.BusMode == CanBusMode.FlexibleDataRate
                    ? "XL_CanTransmitEx"
                    : "XL_CanTransmit";
                string frameType = Options.BusMode == CanBusMode.FlexibleDataRate
                    ? "CAN/CAN FD"
                    : "Classic CAN";
                return ValueTask.FromResult(HardwareOperationResult.Failed(
                    new HardwareFailure(
                        HardwareOperation.Transmit,
                        HardwareErrorCode.TransmitFailed,
                        $"{apiName} threw while transmitting a {frameType} frame: {exception.Message}")));
            }
        }

        public ValueTask<HardwareOperationResult> FlushAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsOpen)
            {
                return ValueTask.FromResult(
                    CreateSessionNotOpenFailure(HardwareOperation.Flush));
            }

            try
            {
                VectorCanFlushResult? result =
                    _resources.FlushCanQueues(cancellationToken);
                if (!result.HasValue)
                {
                    return ValueTask.FromResult(
                        CreateSessionNotOpenFailure(HardwareOperation.Flush));
                }

                if (result.Value.ReceiveStatus.IsSuccess &&
                    result.Value.TransmitStatus.IsSuccess)
                {
                    return ValueTask.FromResult(HardwareOperationResult.Succeeded());
                }

                VectorNativeStatus firstFailure = !result.Value.ReceiveStatus.IsSuccess
                    ? result.Value.ReceiveStatus
                    : result.Value.TransmitStatus;
                string diagnostics =
                    $"XL_FlushReceiveQueue={result.Value.ReceiveStatus.Name} " +
                    $"({result.Value.ReceiveStatus.Code}); " +
                    $"XL_CanFlushTransmitQueue={result.Value.TransmitStatus.Name} " +
                    $"({result.Value.TransmitStatus.Code}).";
                return ValueTask.FromResult(HardwareOperationResult.Failed(
                    new HardwareFailure(
                        HardwareOperation.Flush,
                        HardwareErrorCode.FlushFailed,
                        $"Vector CAN queue flush failed: {diagnostics}",
                        firstFailure.Code)));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return ValueTask.FromResult(HardwareOperationResult.Failed(
                    new HardwareFailure(
                        HardwareOperation.Flush,
                        HardwareErrorCode.FlushFailed,
                        $"Vector CAN queue flush threw: {exception.Message}")));
            }
        }

        public ValueTask<HardwareOperationResult> StopAsync()
        {
            return ValueTask.FromResult(_resources.Cleanup());
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }

        private async IAsyncEnumerable<RoutedCanFrame> ReceiveCanFdAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                VectorCanFdReceiveBatchResult? batch;
                try
                {
                    batch = await Task.Run(
                        () => _resources.ReceiveCanFdEvents(
                            MaximumReceiveBatchSize,
                            cancellationToken),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new HardwareOperationException(
                        new HardwareFailure(
                            HardwareOperation.Receive,
                            HardwareErrorCode.ReceiveFailed,
                            $"XL_CanReceive threw while receiving CAN/CAN FD frames: {exception.Message}"),
                        exception);
                }

                if (!batch.HasValue)
                {
                    yield break;
                }

                VectorCanFdReceiveBatchResult receivedBatch = batch.Value;
                if (receivedBatch.QueueOverflow)
                {
                    throw new HardwareOperationException(new HardwareFailure(
                        HardwareOperation.Receive,
                        HardwareErrorCode.ReceiveFailed,
                        "XL_CanReceive reported a CAN FD queue overflow; one or more events were lost."));
                }

                foreach (VectorCanFdEvent nativeEvent in receivedBatch.Events)
                {
                    if (TryMapCanFdEvent(nativeEvent, out RoutedCanFrame? routedFrame))
                    {
                        yield return routedFrame;
                    }
                }

                if (!receivedBatch.Status.IsSuccess && !receivedBatch.Status.IsQueueEmpty)
                {
                    throw new HardwareOperationException(new HardwareFailure(
                        HardwareOperation.Receive,
                        HardwareErrorCode.ReceiveFailed,
                        $"XL_CanReceive failed with XL_Status {receivedBatch.Status.Name} " +
                        $"({receivedBatch.Status.Code}).",
                        receivedBatch.Status.Code));
                }

                if (receivedBatch.Status.IsQueueEmpty)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(2), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        private bool TryMapCanFdEvent(
            VectorCanFdEvent nativeEvent,
            [NotNullWhen(true)] out RoutedCanFrame? routedFrame)
        {
            routedFrame = null;
            const VectorCanFdEventFlags unsupportedFlags =
                VectorCanFdEventFlags.ErrorFrame |
                VectorCanFdEventFlags.RemoteFrame;
            if ((nativeEvent.Flags & unsupportedFlags) != 0)
            {
                return false;
            }

            bool isFlexibleDataRate =
                (nativeEvent.Flags & VectorCanFdEventFlags.FlexibleDataRate) != 0;
            bool isBitRateSwitchEnabled =
                (nativeEvent.Flags & VectorCanFdEventFlags.BitRateSwitch) != 0;
            if (!isFlexibleDataRate &&
                (nativeEvent.DataLengthCode > (byte)CanDataLengthCode.Bytes8 ||
                 isBitRateSwitchEnabled))
            {
                throw CreateInvalidCanFdReceiveException(
                    $"a non-FD frame with DLC {nativeEvent.DataLengthCode} " +
                    $"and BRS={isBitRateSwitchEnabled}");
            }

            CanDataLengthCode dataLengthCode = (CanDataLengthCode)nativeEvent.DataLengthCode;
            int payloadLength;
            try
            {
                payloadLength = CanFrame.GetPayloadLength(dataLengthCode);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw CreateInvalidCanFdReceiveException(
                    $"DLC {nativeEvent.DataLengthCode}",
                    exception);
            }

            if (nativeEvent.Data.Length < payloadLength)
            {
                throw CreateInvalidCanFdReceiveException(
                    $"DLC {nativeEvent.DataLengthCode} for a " +
                    $"{nativeEvent.Data.Length}-byte buffer");
            }

            CanGatewaySide source;
            if (nativeEvent.ChannelIndex == Options.RxChannel.ChannelIndex)
            {
                source = CanGatewaySide.Rx;
            }
            else if (nativeEvent.ChannelIndex == Options.TxChannel.ChannelIndex)
            {
                source = CanGatewaySide.Tx;
            }
            else
            {
                return false;
            }

            bool isExtendedIdentifier =
                (nativeEvent.RawIdentifier & ExtendedIdentifierFlag) != 0;
            uint identifier = isExtendedIdentifier
                ? nativeEvent.RawIdentifier & CanFrame.MaximumExtendedIdentifier
                : nativeEvent.RawIdentifier;
            CanFrame frame;
            try
            {
                frame = isFlexibleDataRate
                    ? CanFrame.CreateFlexibleDataRate(
                        identifier,
                        isExtendedIdentifier,
                        dataLengthCode,
                        isBitRateSwitchEnabled,
                        nativeEvent.Data.AsSpan(0, payloadLength))
                    : CanFrame.CreateClassic(
                        identifier,
                        isExtendedIdentifier,
                        nativeEvent.Data.AsSpan(0, payloadLength));
            }
            catch (ArgumentException exception)
            {
                throw CreateInvalidCanFdReceiveException(
                    $"identifier 0x{identifier:X}",
                    exception);
            }

            routedFrame = new RoutedCanFrame(source, frame, DateTimeOffset.UtcNow);
            return true;
        }

        private static HardwareOperationException CreateInvalidCanFdReceiveException(
            string details,
            Exception? innerException = null)
        {
            var failure = new HardwareFailure(
                HardwareOperation.Receive,
                HardwareErrorCode.ReceiveFailed,
                $"XL_CanReceive returned an invalid CAN/CAN FD event: {details}.");
            return innerException is null
                ? new HardwareOperationException(failure)
                : new HardwareOperationException(failure, innerException);
        }

        private bool TryMapClassicEvent(
            VectorClassicCanEvent nativeEvent,
            [NotNullWhen(true)] out RoutedCanFrame? routedFrame)
        {
            routedFrame = null;
            const VectorClassicCanEventFlags unsupportedFlags =
                VectorClassicCanEventFlags.ErrorFrame |
                VectorClassicCanEventFlags.RemoteFrame |
                VectorClassicCanEventFlags.TransmitCompleted |
                VectorClassicCanEventFlags.TransmitRequest;
            if ((nativeEvent.Flags & unsupportedFlags) != 0)
            {
                return false;
            }

            if (nativeEvent.DataLength > CanFrame.MaximumClassicPayloadLength ||
                nativeEvent.Data.Length < nativeEvent.DataLength)
            {
                throw new HardwareOperationException(new HardwareFailure(
                    HardwareOperation.Receive,
                    HardwareErrorCode.ReceiveFailed,
                    $"XL_Receive returned an invalid Classic CAN DLC " +
                    $"{nativeEvent.DataLength} for a {nativeEvent.Data.Length}-byte buffer."));
            }

            CanGatewaySide source;
            if (nativeEvent.ChannelIndex == Options.RxChannel.ChannelIndex)
            {
                source = CanGatewaySide.Rx;
            }
            else if (nativeEvent.ChannelIndex == Options.TxChannel.ChannelIndex)
            {
                source = CanGatewaySide.Tx;
            }
            else
            {
                return false;
            }

            bool isExtendedIdentifier =
                (nativeEvent.RawIdentifier & ExtendedIdentifierFlag) != 0;
            uint identifier = isExtendedIdentifier
                ? nativeEvent.RawIdentifier & CanFrame.MaximumExtendedIdentifier
                : nativeEvent.RawIdentifier;
            CanFrame frame;
            try
            {
                frame = CanFrame.CreateClassic(
                    identifier,
                    isExtendedIdentifier,
                    nativeEvent.Data.AsSpan(0, nativeEvent.DataLength));
            }
            catch (ArgumentException exception)
            {
                throw new HardwareOperationException(
                    new HardwareFailure(
                        HardwareOperation.Receive,
                        HardwareErrorCode.ReceiveFailed,
                        $"XL_Receive returned an invalid Classic CAN identifier: " +
                        $"{exception.Message}"),
                    exception);
            }

            routedFrame = new RoutedCanFrame(source, frame, DateTimeOffset.UtcNow);
            return true;
        }

        private static HardwareOperationResult CreateNativeFailure(
            HardwareOperation operation,
            HardwareErrorCode errorCode,
            string apiName,
            VectorNativeStatus status)
        {
            return HardwareOperationResult.Failed(new HardwareFailure(
                operation,
                errorCode,
                $"{apiName} failed with XL_Status {status.Name} ({status.Code}).",
                status.Code));
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
