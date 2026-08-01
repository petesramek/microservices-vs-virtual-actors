namespace Microservices.Tests.Infrastructure;

using Comparison.Contracts;
using Orders.Api.Clients.Abstraction;

/// <summary>
/// Thread-safe fake inventory client used by Orders API tests.
/// </summary>
public sealed class FakeInventoryClient : IInventoryClient {
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, int> _available = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, (string ProductId, int Quantity)> _reservations = [];

    public Task<InventoryResponse> ResetAsync(ResetInventoryRequest request, CancellationToken cancellationToken) {
        lock (_syncRoot) {
            _available[request.ProductId] = request.Quantity;
            _reservations.Clear();
            return Task.FromResult(new InventoryResponse(request.ProductId, request.Quantity));
        }
    }

    public Task<InventoryResponse> GetAsync(string productId, CancellationToken cancellationToken) {
        lock (_syncRoot) {
            return Task.FromResult(new InventoryResponse(productId, _available.GetValueOrDefault(productId)));
        }
    }

    public Task<ReserveInventoryResponse> ReserveAsync(string productId, ReserveInventoryRequest request, CancellationToken cancellationToken) {
        lock (_syncRoot) {
            if (_reservations.ContainsKey(request.ReservationId)) {
                return Task.FromResult(new ReserveInventoryResponse(true, null, _available.GetValueOrDefault(productId)));
            }

            var available = _available.GetValueOrDefault(productId);
            if (available < request.Quantity) {
                return Task.FromResult(new ReserveInventoryResponse(false, "InsufficientInventory", available));
            }

            _available[productId] = available - request.Quantity;
            _reservations[request.ReservationId] = (productId, request.Quantity);
            return Task.FromResult(new ReserveInventoryResponse(true, null, _available[productId]));
        }
    }

    public Task<InventoryResponse> ReleaseAsync(string productId, ReleaseInventoryRequest request, CancellationToken cancellationToken) {
        lock (_syncRoot) {
            if (_reservations.Remove(request.ReservationId, out var reservation)) {
                _available[reservation.ProductId] = _available.GetValueOrDefault(reservation.ProductId) + reservation.Quantity;
            }

            return Task.FromResult(new InventoryResponse(productId, _available.GetValueOrDefault(productId)));
        }
    }
}
