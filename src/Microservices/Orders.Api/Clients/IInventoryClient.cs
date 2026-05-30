using ArchitectureComparison.Contracts;

namespace Orders.Api.Clients;

/// <summary>
/// Inventory service client abstraction.
/// </summary>
public interface IInventoryClient
{
    /// <summary>
    /// Resets inventory for a product.
    /// </summary>
    Task<InventoryResponse> ResetAsync(ResetInventoryRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Gets inventory for a product.
    /// </summary>
    Task<InventoryResponse> GetAsync(string productId, CancellationToken cancellationToken);

    /// <summary>
    /// Reserves inventory.
    /// </summary>
    Task<ReserveInventoryResponse> ReserveAsync(string productId, ReserveInventoryRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Releases inventory.
    /// </summary>
    Task<InventoryResponse> ReleaseAsync(string productId, ReleaseInventoryRequest request, CancellationToken cancellationToken);
}
