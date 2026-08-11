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

        // Native receive failures are surfaced as HardwareOperationException so
        // operation, error code, and native status remain available to callers.
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

}
