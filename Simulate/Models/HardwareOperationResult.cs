using System;

namespace Simulate.Models
{
    public enum HardwareOperation
    {
        DiscoverInterfaces,
        OpenDriver,
        OpenSession,
        ConfigureSession,
        ActivateSession,
        Receive,
        Transmit,
        Flush,
        Stop,
        Dispose
    }

    public enum HardwareErrorCode
    {
        InvalidConfiguration,
        DriverUnavailable,
        DiscoveryFailed,
        OpenFailed,
        ConfigurationFailed,
        ActivationFailed,
        SessionNotOpen,
        ReceiveFailed,
        TransmitFailed,
        FlushFailed,
        StopFailed,
        Unexpected
    }

    public sealed class HardwareFailure
    {
        public HardwareFailure(
            HardwareOperation operation,
            HardwareErrorCode code,
            string message,
            int? nativeStatus = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("A hardware failure message is required.", nameof(message));
            }

            Operation = operation;
            Code = code;
            Message = message;
            NativeStatus = nativeStatus;
        }

        public HardwareOperation Operation { get; }

        public HardwareErrorCode Code { get; }

        public string Message { get; }

        public int? NativeStatus { get; }
    }

    public sealed class HardwareOperationResult
    {
        private HardwareOperationResult(HardwareFailure? failure)
        {
            Failure = failure;
        }

        public bool IsSuccess => Failure is null;

        public HardwareFailure? Failure { get; }

        public static HardwareOperationResult Succeeded()
        {
            return new HardwareOperationResult(failure: null);
        }

        public static HardwareOperationResult Failed(HardwareFailure failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            return new HardwareOperationResult(failure);
        }

        public static HardwareOperationResult<T> Succeeded<T>(T value) where T : notnull
        {
            ArgumentNullException.ThrowIfNull(value);
            return new HardwareOperationResult<T>(value, failure: null);
        }

        public static HardwareOperationResult<T> Failed<T>(HardwareFailure failure) where T : notnull
        {
            ArgumentNullException.ThrowIfNull(failure);
            return new HardwareOperationResult<T>(value: default, failure);
        }
    }

    public sealed class HardwareOperationResult<T> where T : notnull
    {
        internal HardwareOperationResult(T? value, HardwareFailure? failure)
        {
            Value = value;
            Failure = failure;
        }

        public bool IsSuccess => Failure is null;

        public T? Value { get; }

        public HardwareFailure? Failure { get; }
    }
}
