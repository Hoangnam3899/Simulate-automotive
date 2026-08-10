using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simulate.Models;

namespace Simulate.Services
{
    public interface ICanHardwareDriver
    {
        public Task<HardwareOperationResult<IReadOnlyList<HardwareInterface>>> DiscoverInterfacesAsync(
            CancellationToken cancellationToken = default);

        public Task<HardwareOperationResult<ICanGatewaySession>> OpenGatewaySessionAsync(
            CanGatewayOptions options,
            CancellationToken cancellationToken = default);
    }

    public interface ICanGatewaySession : IAsyncDisposable
    {
        // Implementations own every native resource opened for this session. StopAsync
        // and DisposeAsync must both be safe to call repeatedly. StopAsync must complete
        // cleanup once started, and DisposeAsync must ensure equivalent cleanup before
        // releasing the final resource.
        public CanGatewayOptions Options { get; }

        public bool IsOpen { get; }

        public IAsyncEnumerable<RoutedCanFrame> ReceiveAsync(
            CancellationToken cancellationToken = default);

        // A Classic CAN session must return an InvalidConfiguration failure for a CAN FD
        // frame. A CAN FD session accepts both Classic and CAN FD frames.
        public ValueTask<HardwareOperationResult> TransmitAsync(
            CanGatewaySide destination,
            CanFrame frame,
            CancellationToken cancellationToken = default);

        public ValueTask<HardwareOperationResult> FlushAsync(
            CancellationToken cancellationToken = default);

        public ValueTask<HardwareOperationResult> StopAsync();
    }

    // Transitional seam for the current synchronous ConnectionViewModel. Task 6 migrates
    // the caller to ICanHardwareDriver after the Vector and mock session adapters exist.
    public interface ICanConnectionDriver
    {
        public bool IsConnected { get; }

        public List<HardwareInterface> GetAvailableInterfaces();

        public bool Connect(
            HardwareChannel txChannel,
            HardwareChannel rxChannel,
            uint baudrate,
            bool isCanFd);

        public bool Disconnect();
    }
}
