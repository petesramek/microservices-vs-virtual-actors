namespace Orders.Api.Internal.Clients.Abstraction;

using Workbench.Contracts.Inventory;

/// <summary>
/// Defines operations for communicating with the Inventory API.
/// </summary>
public interface IInventoryClient {
    /// <summary>
    /// Resets the available inventory quantity for a product.
    /// </summary>
    /// <param name="request">
    /// The request containing the product identifier and replacement quantity.
    /// </param>
    /// <param name="cancellationToken">
    /// The token that cancels the inventory request.
    /// </param>
    /// <returns>
    /// A task whose result contains the product identifier and available quantity
    /// after the reset.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while the request is in
    /// progress.
    /// </exception>
    Task<InventoryResponse> ResetAsync(
        ResetInventoryRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the current inventory for a product.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="cancellationToken">
    /// The token that cancels the inventory request.
    /// </param>
    /// <returns>
    /// A task whose result contains the product identifier and currently
    /// available quantity.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while the request is in
    /// progress.
    /// </exception>
    Task<InventoryResponse> GetAsync(
        string productId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to reserve inventory for an order.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="request">
    /// The request containing the order, reservation, and quantity values.
    /// </param>
    /// <param name="cancellationToken">
    /// The token that cancels the reservation request.
    /// </param>
    /// <returns>
    /// A task whose result describes whether inventory was reserved and reports
    /// the remaining available quantity.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while the request is in
    /// progress.
    /// </exception>
    Task<ReserveInventoryResponse> ReserveAsync(
        string productId,
        ReserveInventoryRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Releases a previously created inventory reservation.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="request">
    /// The request identifying the inventory reservation to release.
    /// </param>
    /// <param name="cancellationToken">
    /// The token that cancels the release request.
    /// </param>
    /// <returns>
    /// A task whose result contains the product identifier and available quantity
    /// after the release.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while the request is in
    /// progress.
    /// </exception>
    Task<InventoryResponse> ReleaseAsync(
        string productId,
        ReleaseInventoryRequest request,
        CancellationToken cancellationToken);
}
