namespace Workbench.Contracts;

/// <summary>
/// Represents a request to reserve inventory for an order.
/// </summary>
/// <param name="ReservationId">The reservation identifier.</param>
/// <param name="OrderId">The order identifier.</param>
/// <param name="Quantity">The inventory quantity to reserve.</param>
public sealed record ReserveInventoryRequest(
    Guid ReservationId,
    Guid OrderId,
    int Quantity);
