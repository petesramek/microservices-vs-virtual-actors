namespace ArchitectureComparison.Contracts;

/// <summary>
/// Request used to reset inventory for deterministic scenarios.
/// </summary>
/// <param name="ProductId">The product identifier.</param>
/// <param name="Quantity">The quantity to set.</param>
public sealed record ResetInventoryRequest(string ProductId, int Quantity);

/// <summary>
/// Inventory response.
/// </summary>
/// <param name="ProductId">The product identifier.</param>
/// <param name="AvailableQuantity">The available quantity.</param>
public sealed record InventoryResponse(string ProductId, int AvailableQuantity);

/// <summary>
/// Inventory reservation request.
/// </summary>
/// <param name="ReservationId">The reservation identifier.</param>
/// <param name="OrderId">The order identifier.</param>
/// <param name="Quantity">The quantity to reserve.</param>
public sealed record ReserveInventoryRequest(Guid ReservationId, Guid OrderId, int Quantity);

/// <summary>
/// Inventory release request.
/// </summary>
/// <param name="ReservationId">The reservation identifier.</param>
public sealed record ReleaseInventoryRequest(Guid ReservationId);

/// <summary>
/// Inventory reservation response.
/// </summary>
/// <param name="Reserved">A value indicating whether inventory was reserved.</param>
/// <param name="Reason">The rejection reason, when applicable.</param>
/// <param name="AvailableQuantity">The remaining available quantity.</param>
public sealed record ReserveInventoryResponse(bool Reserved, string? Reason, int AvailableQuantity);

/// <summary>
/// Payment authorization request.
/// </summary>
/// <param name="PaymentId">The payment identifier.</param>
/// <param name="OrderId">The order identifier.</param>
/// <param name="CustomerId">The customer identifier.</param>
/// <param name="IdempotencyKey">The idempotency key.</param>
/// <param name="SimulateFailure">A value indicating whether failure should be simulated.</param>
public sealed record AuthorizePaymentRequest(Guid PaymentId, Guid OrderId, string CustomerId, string IdempotencyKey, bool SimulateFailure);

/// <summary>
/// Payment authorization response.
/// </summary>
/// <param name="Authorized">A value indicating whether payment was authorized.</param>
/// <param name="Reason">The failure reason, when applicable.</param>
public sealed record AuthorizePaymentResponse(bool Authorized, string? Reason);

/// <summary>
/// Externally visible order response.
/// </summary>
/// <param name="OrderId">The order identifier.</param>
/// <param name="Status">The order status.</param>
/// <param name="Reason">The rejection reason, when applicable.</param>
public sealed record OrderResponse(Guid OrderId, OrderStatus Status, string? Reason);
